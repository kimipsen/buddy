#!/usr/bin/env bash
# shellcheck disable=SC2016 # the sh -c scripts expand inside the container, and jq has its own $vars
# Copies the production data of the Azure Container Apps deployment (deploy/azure) into the
# docker-compose stack on this machine (docker-compose.prod.yml): the app database (Marten
# events, snapshots, erasure ledger) and the keycloak database (realm, clients, users,
# passwords). See deploy/README.md "Moving from Azure".
#
# Run it on the new server, from a checkout with both deploy/.env (this stack) and
# deploy/azure/.env (the Azure one), after a first `task deploy:pull` of the release Azure runs.
# It needs az (logged in), docker, jq and curl.
#
# What it does:
#   1. runs preflight.sh (not in a container, DEPLOY_HOST is this machine, real passwords)
#   2. opens the Azure Postgres firewall to this machine's public IP (removed again on exit)
#   3. deactivates the Azure api and keycloak revisions so nothing writes while it dumps
#      (--no-freeze skips this), then pg_dumps both databases into the dump directory
#   4. reactivates Azure, unless --move, which leaves it stopped
#   5. dumps this stack's databases as a safety copy, replaces them with the Azure dumps and
#      rewrites the Azure hostnames in the buddy realm's clients to API/AUTH/APP_DOMAIN
#   6. starts the stack again
#
# Usage: deploy/migrate-from-azure.sh [--move] [--no-freeze] [--yes]
#                                     [--azure-env FILE] [--dump-dir DIR]
set -euo pipefail

dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$dir"

compose=(docker compose -f docker-compose.prod.yml)
azure_env="$dir/azure/.env"
dump_root="$HOME/buddy-backups"
move=false
freeze=true
assume_yes=false

usage() { sed -n '3,/^set -euo/p' "${BASH_SOURCE[0]}" | sed '$d' | sed 's/^# \{0,1\}//'; }

while [[ $# -gt 0 ]]; do
  case $1 in
    --move) move=true ;;
    --no-freeze) freeze=false ;;
    --yes) assume_yes=true ;;
    --azure-env) azure_env=$2; shift ;;
    --dump-dir) dump_root=$2; shift ;;
    -h|--help) usage; exit 0 ;;
    *) echo "migrate: unknown option $1" >&2; usage >&2; exit 2 ;;
  esac
  shift
done

log() { echo "==> $*"; }
warn() { echo "!! $*" >&2; }
fail() { echo "migrate: $*" >&2; exit 1; }

# Same dotenv reading as preflight.sh: last assignment wins, quotes and CR stripped.
value_of() {
  local key=$1 file=$2 v
  v=$(sed -n -E "s/^[[:space:]]*(export[[:space:]]+)?${key}[[:space:]]*=(.*)$/\2/p" "$file" | tail -n1)
  v=${v%$'\r'}
  v=${v#\"}; v=${v%\"}; v=${v#\'}; v=${v%\'}
  printf '%s' "$v"
}

for cmd in az docker jq curl; do
  command -v "$cmd" >/dev/null || fail "$cmd is required - install it and rerun."
done
[[ -f "$azure_env" ]] || fail "$azure_env not found. Copy the deploy/azure/.env you deploy Azure with to this machine."
[[ $freeze == true || $move == false ]] || fail "--move needs the freeze; drop --no-freeze."

# Refuses inside a container, unless DEPLOY_HOST is this machine, and on placeholder passwords.
./preflight.sh

# --- Settings ------------------------------------------------------------------------------------

rg=$(value_of RESOURCE_GROUP "$azure_env")
pg_server=$(value_of PG_SERVER_NAME "$azure_env")
pg_user=$(value_of PG_ADMIN_USER "$azure_env")
pg_password=$(value_of PG_ADMIN_PASSWORD "$azure_env")
azure_app_db=$(value_of APP_DB_NAME "$azure_env")
azure_app_db=${azure_app_db:-buddy}
kc_admin=$(value_of KEYCLOAK_ADMIN "$azure_env")
kc_admin_password=$(value_of KEYCLOAK_ADMIN_PASSWORD "$azure_env")
for key in RESOURCE_GROUP PG_SERVER_NAME PG_ADMIN_USER PG_ADMIN_PASSWORD KEYCLOAK_ADMIN KEYCLOAK_ADMIN_PASSWORD; do
  [[ -n "$(value_of "$key" "$azure_env")" ]] || fail "$key is not set in $azure_env."
done
pg_host="$pg_server.postgres.database.azure.com"

local_db=$(value_of POSTGRES_DB .env)
api_domain=$(value_of API_DOMAIN .env)
auth_domain=$(value_of AUTH_DOMAIN .env)
app_domain=$(value_of APP_DOMAIN .env)
local_cli_secret=$(value_of KEYCLOAK_ADMIN_CLI_SECRET .env)
for key in POSTGRES_DB API_DOMAIN AUTH_DOMAIN APP_DOMAIN; do
  [[ -n "$(value_of "$key" .env)" ]] || fail "$key is not set in deploy/.env."
done

az account show -o none 2>/dev/null || fail "not logged in to Azure. Run az login (and az account set) first."
az postgres flexible-server show --name "$pg_server" --resource-group "$rg" -o none \
  || fail "can't find the Postgres server $pg_server in $rg."

fqdn() {
  az containerapp show --name "$1" --resource-group "$rg" --query properties.configuration.ingress.fqdn -o tsv 2>/dev/null || true
}
azure_frontend=$(fqdn frontend)
azure_api=$(fqdn api)
azure_keycloak=$(fqdn keycloak)

# Every Azure hostname a client or the realm may mention, paired with its new one here.
host_pairs=()
add_pair() { [[ -n "$1" && "$1" != "$2" ]] && host_pairs+=("https://$1" "https://$2"); return 0; }
add_pair "$azure_frontend" "$app_domain"
add_pair "$(value_of FRONTEND_CUSTOM_DOMAIN "$azure_env")" "$app_domain"
add_pair "$azure_api" "$api_domain"
add_pair "$(value_of API_CUSTOM_DOMAIN "$azure_env")" "$api_domain"
add_pair "$azure_keycloak" "$auth_domain"
add_pair "$(value_of KEYCLOAK_CUSTOM_DOMAIN "$azure_env")" "$auth_domain"

azure_api_public=$(value_of API_CUSTOM_DOMAIN "$azure_env")
azure_api_public=${azure_api_public:-$azure_api}
azure_version=$(curl -fsS --max-time 10 "https://$azure_api_public/version" 2>/dev/null | jq -r '.version // empty' 2>/dev/null || true)

# The images must already be here: the restore starts the stack with --no-build.
for image in $("${compose[@]}" config --images); do
  docker image inspect "$image" >/dev/null 2>&1 \
    || fail "image $image isn't on this machine. Deploy the release Azure runs here first:
  git checkout v${azure_version:-<version>} && task deploy:pull"
done

# --- Confirm -------------------------------------------------------------------------------------

ts=$(date +%Y%m%d-%H%M%S)
dump_dir="$dump_root/azure-migration-$ts"

cat <<EOF

Copy Buddy's data from Azure to this machine

  From: $pg_host (databases $azure_app_db, keycloak), resource group $rg
        Azure release: ${azure_version:-unknown}
  To:   this machine's docker-compose stack (databases $local_db, keycloak)
        https://$app_domain, https://$api_domain, https://$auth_domain
  Dumps: $dump_dir

  - The data on this machine is REPLACED (a dump of it is taken first).
EOF
if [[ $freeze == true ]]; then
  echo "  - Azure's api and keycloak are stopped while the databases are dumped (a few minutes)."
else
  echo "  - Azure keeps running (--no-freeze): writes made during the dump can be lost or half copied."
fi
if [[ $move == true ]]; then
  echo "  - --move: Azure's api and keycloak STAY stopped afterwards."
fi
echo

if [[ $assume_yes != true ]]; then
  read -r -p "Type MIGRATE to continue: " answer
  [[ "$answer" == MIGRATE ]] || fail "cancelled."
fi

# --- Cleanup on exit -----------------------------------------------------------------------------

firewall_rule=""
frozen=()   # app:revision pairs this script deactivated
succeeded=false

reactivate_azure() {
  local pair
  for pair in "${frozen[@]}"; do
    log "Reactivating Azure ${pair%%:*} (revision ${pair#*:})"
    az containerapp revision activate --name "${pair%%:*}" --resource-group "$rg" --revision "${pair#*:}" -o none \
      || warn "couldn't reactivate ${pair#*:}: az containerapp revision activate --name ${pair%%:*} --resource-group $rg --revision ${pair#*:}"
  done
  frozen=()
}

cleanup() {
  local status=$?
  if [[ ${#frozen[@]} -gt 0 && ( $move != true || $succeeded != true ) ]]; then
    reactivate_azure
  fi
  if [[ -n "$firewall_rule" ]]; then
    log "Removing the temporary Postgres firewall rule $firewall_rule"
    az postgres flexible-server firewall-rule delete --resource-group "$rg" --server-name "$pg_server" \
      --name "$firewall_rule" --yes -o none \
      || warn "couldn't remove the firewall rule $firewall_rule - remove it in the Azure portal (Networking)."
  fi
  if [[ $status -ne 0 ]]; then
    warn "migration failed (exit $status). Dumps so far are in $dump_dir."
  fi
}
trap cleanup EXIT

umask 077
mkdir -p "$dump_dir"

# --- Local stack: database up, safety dump -------------------------------------------------------

log "Starting this machine's db"
"${compose[@]}" up -d --no-build --wait db

local_schemas=$("${compose[@]}" exec -T db sh -c 'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -At -c "$1"' sh \
  "select count(*) from information_schema.schemata where schema_name not in ('public', 'information_schema') and schema_name not like 'pg\\_%'")
if [[ "$local_schemas" != 0 ]]; then
  log "This machine already has data; dumping it to $dump_dir/local-before-*.dump"
  "${compose[@]}" exec -T db sh -c 'pg_dump -U "$POSTGRES_USER" -Fc "$POSTGRES_DB"' > "$dump_dir/local-before-app.dump"
  "${compose[@]}" exec -T db sh -c 'pg_dump -U "$POSTGRES_USER" -Fc keycloak' > "$dump_dir/local-before-keycloak.dump"
fi

# --- Azure: firewall, freeze, dump ---------------------------------------------------------------

my_ip=$(curl -fsS --max-time 10 https://api.ipify.org) || fail "couldn't find this machine's public IP."
[[ "$my_ip" =~ ^[0-9]+\.[0-9]+\.[0-9]+\.[0-9]+$ ]] || fail "unexpected public IP '$my_ip'."
firewall_rule="buddy-migrate-$ts"
log "Allowing $my_ip through the Azure Postgres firewall (rule $firewall_rule)"
az postgres flexible-server firewall-rule create --resource-group "$rg" --server-name "$pg_server" \
  --name "$firewall_rule" --start-ip-address "$my_ip" --end-ip-address "$my_ip" -o none

if [[ $freeze == true ]]; then
  for app in api keycloak; do
    for revision in $(az containerapp revision list --name "$app" --resource-group "$rg" --query "[?properties.active].name" -o tsv); do
      log "Deactivating Azure $app (revision $revision)"
      az containerapp revision deactivate --name "$app" --resource-group "$rg" --revision "$revision" -o none
      frozen+=("$app:$revision")
    done
  done
  log "Waiting 30s for in-flight requests to finish"
  sleep 30
fi

# pg_dump runs in the local db container (Postgres 18 client; Azure runs 16). The password goes in
# on stdin, so it isn't on any command line.
dump_azure() {
  local db=$1 out=$2
  log "Dumping Azure database $db"
  printf '%s\n' "$pg_password" | "${compose[@]}" exec -T db sh -c \
    'IFS= read -r PGPASSWORD; export PGPASSWORD; exec pg_dump -Fc "host=$1 port=5432 user=$2 dbname=$3 sslmode=require"' \
    sh "$pg_host" "$pg_user" "$db" > "$out"
  # Fails on a truncated archive.
  "${compose[@]}" exec -T db pg_restore --list < "$out" > /dev/null
}
dump_azure "$azure_app_db" "$dump_dir/azure-app.dump"
dump_azure keycloak "$dump_dir/azure-keycloak.dump"

if [[ $move != true ]]; then
  reactivate_azure
fi

# --- Local: restore ------------------------------------------------------------------------------

log "Stopping this machine's api and keycloak"
"${compose[@]}" stop api keycloak

restore_db() {
  local db=$1 file=$2
  log "Restoring $file into $db"
  "${compose[@]}" exec -T db sh -c \
    'psql -U "$POSTGRES_USER" -d postgres -v ON_ERROR_STOP=1 -c "DROP DATABASE IF EXISTS \"$1\" WITH (FORCE)" -c "CREATE DATABASE \"$1\""' \
    sh "$db"
  # Azure's roles (azure_pg_admin, the admin user) don't exist here: everything is owned by
  # POSTGRES_USER, which is also the user Keycloak connects as.
  "${compose[@]}" exec -T db sh -c 'pg_restore -U "$POSTGRES_USER" -d "$1" --no-owner --no-privileges --exit-on-error' \
    sh "$db" < "$file"
}
restore_db "$local_db" "$dump_dir/azure-app.dump"
restore_db keycloak "$dump_dir/azure-keycloak.dump"

# --- Keycloak: new hostnames ---------------------------------------------------------------------

log "Starting keycloak"
"${compose[@]}" up -d --no-build --wait keycloak

kc() { "${compose[@]}" exec -T keycloak /opt/keycloak/bin/kcadm.sh "$@" --config /tmp/kcadm-migrate.config; }

# The master realm came from Azure too, so its admin signs in with Azure's password.
if ! kc config credentials --server http://localhost:8080 --realm master \
    --user "$kc_admin" --password "$kc_admin_password" >/dev/null 2>&1; then
  warn "couldn't sign in to Keycloak as $kc_admin with the password from $azure_env (changed in the console?).
  Update the buddy realm's clients by hand: Valid redirect URIs, Web origins and Valid post logout
  redirect URIs must name https://$app_domain instead of the Azure hostnames."
else
  pairs_json='[]'
  if [[ ${#host_pairs[@]} -gt 0 ]]; then
    pairs_json=$(printf '%s\n' "${host_pairs[@]}" | jq -R . | jq -sc '[range(0; length; 2) as $i | [.[$i], .[$i + 1]]]')
  fi
  # Literal (not regex) replacement in every string of the representation.
  rewrite='walk(if type == "string" then reduce $pairs[] as $p (.; split($p[0]) | join($p[1])) else . end)'

  kc get clients -r buddy | jq -c '.[]' | while IFS= read -r client; do
    updated=$(jq -c --argjson pairs "$pairs_json" "$rewrite" <<<"$client")
    if [[ "$updated" != "$client" ]]; then
      log "Pointing Keycloak client $(jq -r .clientId <<<"$client") at the new domains"
      kc update "clients/$(jq -r .id <<<"$client")" -r buddy -f - <<<"$updated"
    fi
  done

  realm=$(kc get realms/buddy)
  updated=$(jq -c --argjson pairs "$pairs_json" "$rewrite" <<<"$realm")
  if [[ "$updated" != "$(jq -c . <<<"$realm")" ]]; then
    log "Pointing the buddy realm's settings at the new domains"
    kc update realms/buddy -f - <<<"$updated"
  fi

  # The client secret lives in the keycloak database, so the API must use Azure's one now.
  cli_id=$(kc get clients -r buddy -q clientId=buddy-admin-cli --fields id | jq -r '.[0].id // empty')
  if [[ -n "$cli_id" ]]; then
    cli_secret=$(kc get "clients/$cli_id/client-secret" -r buddy | jq -r '.value // empty')
    if [[ "$cli_secret" != "$local_cli_secret" ]]; then
      warn "KEYCLOAK_ADMIN_CLI_SECRET in deploy/.env doesn't match the buddy-admin-cli secret that came
  from Azure. Copy it from Keycloak (Clients > buddy-admin-cli > Credentials) or from $azure_env
  into deploy/.env, then: docker compose -f docker-compose.prod.yml up -d --no-build api"
    fi
  fi
  "${compose[@]}" exec -T keycloak rm -f /tmp/kcadm-migrate.config || true
fi

# --- Start everything ----------------------------------------------------------------------------

log "Starting the whole stack"
"${compose[@]}" up -d --no-build --wait --wait-timeout 600
succeeded=true

if [[ -n "$(value_of BREVO_SMTP_KEY "$azure_env")" && -z "$(value_of Mail__Host .env)" ]]; then
  warn "Azure sends the API's email through Brevo, but deploy/.env has no Mail__* settings. Add
  Mail__Host, Mail__Port, Mail__UseSsl, Mail__Credentials__Username, Mail__Credentials__Password,
  Mail__FromAddress and Mail__FromName (deploy/azure/deploy.sh lists the values), then restart api.
  Keycloak's own mail settings came over with the realm."
fi

cat <<EOF

==> Done. The data from Azure is now in this machine's stack.

  Dumps: $dump_dir (personal data: copy them off this machine or delete them,
         and delete them after 30 days at the latest)

Next:
  - Check it as in deploy/README.md step 6, then sign in to https://$app_domain.
  - The Keycloak admin console (https://$auth_domain) uses Azure's admin password now.
EOF
if [[ $move == true ]]; then
  cat <<EOF
  - Azure's api and keycloak are stopped. When you're sure, delete the Azure resources
    (az group delete --name $rg; that also deletes Azure's own point-in-time backups), or
    start them again with: az containerapp revision activate --name <api|keycloak> --resource-group $rg --revision <name>
EOF
else
  echo "  - Azure is running again; anything written there from now on is NOT on this machine."
  echo "    Rerun with --move when you switch over for good."
fi
