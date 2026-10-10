#!/usr/bin/env bash
# Deploys backend + frontend + Keycloak + Postgres to Azure Container Apps.
# See ../../README-azure.md for the full walkthrough.
#
# Usage: cp .env.example .env, fill in real values, then run this script
# from anywhere (paths below are resolved relative to the repo root).
#
# Custom domains: FRONTEND_CUSTOM_DOMAIN / API_CUSTOM_DOMAIN /
# KEYCLOAK_CUSTOM_DOMAIN in .env are optional and blank by default, so a
# first run just uses the *.azurecontainerapps.io domains Azure assigns.
# Fill them in later (see "Custom domains" in README-azure.md for the DNS
# records to create first) and rerun this script to bind them.

set -euo pipefail

command -v jq >/dev/null || { echo "jq is required (used to configure Keycloak's SMTP settings via its Admin REST API) - install it and rerun." >&2; exit 1; }

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"

# Build version from git tags (docs/versioning.md), baked into the API and frontend images.
BUDDY_VERSION="$("$REPO_ROOT/deploy/version.sh")"
BUDDY_COMMIT="$("$REPO_ROOT/deploy/version.sh" --commit)"

# shellcheck disable=SC1091
source "$SCRIPT_DIR/.env"

# Optional - blank means "use the Azure-assigned default domain".
: "${FRONTEND_CUSTOM_DOMAIN:=}"
: "${API_CUSTOM_DOMAIN:=}"
: "${KEYCLOAK_CUSTOM_DOMAIN:=}"

# Warns (never fails) when this commit is missing the newest release tag.
"$REPO_ROOT/deploy/release-check.sh" "${REPOSITORY_URL:-}" || true

# Outbound email (Brevo SMTP relay) - optional, blank means the API/Keycloak
# deploy without it. See "Mail" in README-azure.md for how to get an SMTP key.
: "${BREVO_SMTP_LOGIN:=}"
: "${BREVO_SMTP_KEY:=}"
: "${MAIL_FROM_ADDRESS:=}"
: "${MAIL_FROM_NAME:=Buddy}"
BREVO_SMTP_HOST=smtp-relay.brevo.com
BREVO_SMTP_PORT=587

containerapp_exists() {
  local name=$1
  az containerapp show --name "$name" --resource-group "$RESOURCE_GROUP" -o none 2>/dev/null
}

containerapp_env_exists() {
  az containerapp env show --name "$CONTAINERAPPS_ENV" --resource-group "$RESOURCE_GROUP" -o none 2>/dev/null
}

postgres_server_exists() {
  az postgres flexible-server show --name "$PG_SERVER_NAME" --resource-group "$RESOURCE_GROUP" -o none 2>/dev/null
}

postgres_firewall_rule_exists() {
  local rule_name=$1
  az postgres flexible-server firewall-rule show --resource-group "$RESOURCE_GROUP" \
    --server-name "$PG_SERVER_NAME" --name "$rule_name" -o none 2>/dev/null
}

# Sets a container app's health probes. The containerapp CLI has no probe flags, so this patches
# them through ARM. PATCH replaces the whole containers array, so the current container goes back
# with only its probes changed. Only the fields given are compared (Azure fills in defaults), so an
# unchanged config adds no revision. Newer containerapp CLI versions return imageType, which the
# pinned api-version rejects, so it is dropped before the PATCH.
set_probes() {
  local name=$1 probes=$2 app_id containers
  app_id=$(az containerapp show --name "$name" --resource-group "$RESOURCE_GROUP" --query id -o tsv)
  containers=$(az containerapp show --name "$name" --resource-group "$RESOURCE_GROUP" \
    --query properties.template.containers -o json)
  if [[ $(echo "$containers" | jq --argjson probes "$probes" '
      [.[0].probes // [] | .[] | {type, httpGet: {path: .httpGet.path, port: .httpGet.port},
        periodSeconds, timeoutSeconds, failureThreshold}] | sort_by(.type)
      == ($probes | sort_by(.type))') != true ]]; then
    az rest --method patch \
      --url "https://management.azure.com${app_id}?api-version=2024-03-01" \
      --body "$(echo "$containers" | jq --argjson probes "$probes" \
        '{properties: {template: {containers: (map(del(.imageType)) | .[0].probes = $probes)}}}')" \
      -o none
  fi
}

postgres_db_exists() {
  local db_name=$1
  az postgres flexible-server db show --resource-group "$RESOURCE_GROUP" \
    --server-name "$PG_SERVER_NAME" --name "$db_name" -o none 2>/dev/null
}

bind_custom_domain() {
  local app_name=$1 domain=$2 default_fqdn=$3
  if [[ -z "$domain" ]]; then
    return
  fi
  echo "==> Binding custom domain $domain to $app_name"
  if ! az containerapp hostname list --name "$app_name" --resource-group "$RESOURCE_GROUP" \
        --query "[?name=='$domain']" -o tsv | grep -q .; then
    az containerapp hostname add --hostname "$domain" --name "$app_name" \
      --resource-group "$RESOURCE_GROUP" -o none
  fi
  if ! az containerapp hostname bind --hostname "$domain" --name "$app_name" \
        --resource-group "$RESOURCE_GROUP" --environment "$CONTAINERAPPS_ENV" -o none; then
    cat <<WARN
    !! Could not bind $domain yet. Create a CNAME record for $domain pointing
       at $default_fqdn, and a TXT record for asuid.$domain with the value
       from:
         az containerapp show --name $app_name --resource-group $RESOURCE_GROUP --query properties.customDomainVerificationId -o tsv
       then rerun this script once the records have propagated.
WARN
  fi
}

echo "==> Registering resource providers and the containerapp CLI extension"
az extension add --name containerapp --upgrade -o none -y
az provider register --namespace Microsoft.App -o none
az provider register --namespace Microsoft.OperationalInsights -o none
az provider register --namespace Microsoft.DBforPostgreSQL -o none
az provider register --namespace Microsoft.ContainerRegistry -o none

echo "==> Creating resource group $RESOURCE_GROUP"
az group create --name "$RESOURCE_GROUP" --location "$LOCATION" -o none

echo "==> Creating container registry $ACR_NAME"
az acr create \
  --resource-group "$RESOURCE_GROUP" \
  --name "$ACR_NAME" \
  --sku Basic \
  --admin-enabled true \
  -o none

ACR_SERVER="$ACR_NAME.azurecr.io"
ACR_USERNAME=$(az acr credential show --name "$ACR_NAME" --query username -o tsv)
ACR_PASSWORD=$(az acr credential show --name "$ACR_NAME" --query "passwords[0].value" -o tsv)

if postgres_server_exists; then
  echo "==> PostgreSQL Flexible Server $PG_SERVER_NAME already exists, skipping creation"
else
  echo "==> Creating PostgreSQL Flexible Server $PG_SERVER_NAME"
  az postgres flexible-server create \
    --resource-group "$RESOURCE_GROUP" \
    --name "$PG_SERVER_NAME" \
    --location "$LOCATION" \
    --admin-user "$PG_ADMIN_USER" \
    --admin-password "$PG_ADMIN_PASSWORD" \
    --sku-name Standard_B1ms \
    --tier Burstable \
    --storage-size 32 \
    --version 16 \
    --yes \
    -o none
fi

# Why public access at all: the Container Apps environment below is not
# VNet-integrated, so keycloak and api reach Postgres over its public endpoint,
# limited to Azure-originated traffic by the AllowAzureServices firewall rule
# (and firewall rules only apply while public access is Enabled). Nothing in
# this script connects to Postgres from the machine running it - the databases
# are created through the Azure control plane (`az postgres flexible-server db
# create`) - so public access is a runtime need of the apps, not of the script.
#
# Only change it when it's actually off, so a rerun doesn't issue a redundant
# server update, and leave a VNet-integrated (private access) server alone:
# public access can't be enabled on one, and the apps would reach it over the
# VNet anyway. If the state can't be read, fall back to enabling it (the
# previous unconditional behaviour). See "Postgres network exposure" in
# README-azure.md.
PG_PUBLIC_ACCESS=$(az postgres flexible-server show \
  --resource-group "$RESOURCE_GROUP" \
  --name "$PG_SERVER_NAME" \
  --query network.publicNetworkAccess -o tsv 2>/dev/null || true)
PG_DELEGATED_SUBNET=$(az postgres flexible-server show \
  --resource-group "$RESOURCE_GROUP" \
  --name "$PG_SERVER_NAME" \
  --query network.delegatedSubnetResourceId -o tsv 2>/dev/null || true)
if [[ -n "$PG_DELEGATED_SUBNET" ]]; then
  echo "==> Postgres server is VNet-integrated (private access) - leaving its network settings alone"
elif [[ "$PG_PUBLIC_ACCESS" == "Enabled" ]]; then
  echo "==> Postgres server already allows public network access (firewalled to Azure services), skipping"
else
  echo "==> Enabling public network access on the Postgres server (was: ${PG_PUBLIC_ACCESS:-unknown}) so the Container Apps can reach it"
  az postgres flexible-server update \
    --resource-group "$RESOURCE_GROUP" \
    --name "$PG_SERVER_NAME" \
    --public-access Enabled \
    -o none
fi

if postgres_firewall_rule_exists AllowAzureServices; then
  echo "==> Firewall rule AllowAzureServices already exists, skipping"
else
  echo "==> Allowing Azure services through the Postgres firewall"
  az postgres flexible-server firewall-rule create \
    --resource-group "$RESOURCE_GROUP" \
    --server-name "$PG_SERVER_NAME" \
    --name AllowAzureServices \
    --start-ip-address 0.0.0.0 \
    --end-ip-address 0.0.0.0 \
    -o none
fi

if postgres_db_exists "$APP_DB_NAME"; then
  echo "==> Database $APP_DB_NAME already exists, skipping"
else
  echo "==> Creating database $APP_DB_NAME"
  az postgres flexible-server db create \
    --resource-group "$RESOURCE_GROUP" \
    --server-name "$PG_SERVER_NAME" \
    --name "$APP_DB_NAME" \
    -o none
fi

if postgres_db_exists keycloak; then
  echo "==> Database keycloak already exists, skipping"
else
  echo "==> Creating database keycloak"
  az postgres flexible-server db create \
    --resource-group "$RESOURCE_GROUP" \
    --server-name "$PG_SERVER_NAME" \
    --name keycloak \
    -o none
fi

PG_HOST="$PG_SERVER_NAME.postgres.database.azure.com"

if containerapp_env_exists; then
  echo "==> Container Apps environment $CONTAINERAPPS_ENV already exists, skipping creation"
else
  echo "==> Creating Container Apps environment $CONTAINERAPPS_ENV"
  az containerapp env create \
    --name "$CONTAINERAPPS_ENV" \
    --resource-group "$RESOURCE_GROUP" \
    --location "$LOCATION" \
    -o none
fi

# Container Apps FQDNs are deterministic: <app-name>.<environment default domain>.
# Computing them up front lets every app be created once, with its peers'
# final URLs already baked in, instead of create-then-update passes.
DEFAULT_DOMAIN=$(az containerapp env show \
  --name "$CONTAINERAPPS_ENV" \
  --resource-group "$RESOURCE_GROUP" \
  --query properties.defaultDomain -o tsv)
KEYCLOAK_FQDN="keycloak.$DEFAULT_DOMAIN"
API_FQDN="api.$DEFAULT_DOMAIN"
FRONTEND_FQDN="frontend.$DEFAULT_DOMAIN"

# The hostname baked into env vars/build args/printed URLs - the custom
# domain once it's configured in .env, otherwise the default FQDN above.
KEYCLOAK_HOSTNAME="${KEYCLOAK_CUSTOM_DOMAIN:-$KEYCLOAK_FQDN}"
API_HOSTNAME="${API_CUSTOM_DOMAIN:-$API_FQDN}"
FRONTEND_HOSTNAME="${FRONTEND_CUSTOM_DOMAIN:-$FRONTEND_FQDN}"

echo "==> Building the Keycloak image (base image + buddy theme)"
az acr build \
  --registry "$ACR_NAME" \
  --image buddy-keycloak:latest \
  --build-arg HEALTH_ENABLED=true \
  --file "$REPO_ROOT/deploy/azure/keycloak/Dockerfile" \
  "$REPO_ROOT"

# Container Apps doesn't reliably roll out a new revision on `update --image`
# when the tag string is unchanged (":latest" every time) - pin to this
# build's immutable digest instead so every deploy actually takes effect.
KEYCLOAK_DIGEST=$(az acr repository show --name "$ACR_NAME" --image buddy-keycloak:latest --query digest -o tsv)
KEYCLOAK_IMAGE="$ACR_SERVER/buddy-keycloak@$KEYCLOAK_DIGEST"

KEYCLOAK_ENV_VARS=(
  KC_DB=postgres
  "KC_DB_URL_HOST=$PG_HOST"
  KC_DB_URL_PORT=5432
  KC_DB_URL_DATABASE=keycloak
  "KC_DB_URL_PROPERTIES=?sslmode=require"
  "KC_DB_USERNAME=$PG_ADMIN_USER"
  KC_DB_PASSWORD=secretref:pg-password
  # Keycloak 26 (hostname v2) takes the scheme from KC_HOSTNAME when it's a
  # full URL; a bare hostname makes it use the request's scheme, which is
  # plain HTTP behind the Container Apps ingress (mixed-content errors in the
  # admin console). KC_PROXY=edge was removed in 26 - trust X-Forwarded-*.
  "KC_HOSTNAME=https://$KEYCLOAK_HOSTNAME"
  KC_HTTP_ENABLED=true
  KC_PROXY_HEADERS=xforwarded
  # KEYCLOAK_ADMIN/_PASSWORD are deprecated in 26; only read on first boot.
  "KC_BOOTSTRAP_ADMIN_USERNAME=$KEYCLOAK_ADMIN"
  KC_BOOTSTRAP_ADMIN_PASSWORD=secretref:keycloak-admin-password
)

if containerapp_exists keycloak; then
  echo "==> Updating Keycloak app config ($KEYCLOAK_HOSTNAME)"
  az containerapp secret set \
    --name keycloak \
    --resource-group "$RESOURCE_GROUP" \
    --secrets \
      "pg-password=$PG_ADMIN_PASSWORD" \
      "keycloak-admin-password=$KEYCLOAK_ADMIN_PASSWORD" \
    -o none
  az containerapp update \
    --name keycloak \
    --resource-group "$RESOURCE_GROUP" \
    --image "$KEYCLOAK_IMAGE" \
    --set-env-vars "${KEYCLOAK_ENV_VARS[@]}" \
    --remove-env-vars KC_PROXY KEYCLOAK_ADMIN KEYCLOAK_ADMIN_PASSWORD \
    -o none
else
  echo "==> Deploying Keycloak ($KEYCLOAK_HOSTNAME)"
  az containerapp create \
    --name keycloak \
    --resource-group "$RESOURCE_GROUP" \
    --environment "$CONTAINERAPPS_ENV" \
    --image "$KEYCLOAK_IMAGE" \
    --registry-server "$ACR_SERVER" \
    --registry-username "$ACR_USERNAME" \
    --registry-password "$ACR_PASSWORD" \
    --target-port 8080 \
    --ingress external \
    --min-replicas 1 --max-replicas 1 \
    --cpu 1.0 --memory 2.0Gi \
    --secrets \
      "pg-password=$PG_ADMIN_PASSWORD" \
      "keycloak-admin-password=$KEYCLOAK_ADMIN_PASSWORD" \
    --env-vars "${KEYCLOAK_ENV_VARS[@]}" \
    -o none
fi

# Keycloak's health endpoints are on the management port (9000), which the ingress (8080) doesn't
# expose, so they stay private. /health/ready includes its database check. A cold start on one CPU
# (realm caches, theme) can take a few minutes, hence the startup probe's 5 min allowance.
echo "==> Configuring Keycloak's health probes"
set_probes keycloak '[
  {"type": "Startup",   "httpGet": {"path": "/health/started", "port": 9000}, "periodSeconds": 10, "timeoutSeconds": 5, "failureThreshold": 30},
  {"type": "Liveness",  "httpGet": {"path": "/health/live",    "port": 9000}, "periodSeconds": 15, "timeoutSeconds": 5, "failureThreshold": 3},
  {"type": "Readiness", "httpGet": {"path": "/health/ready",   "port": 9000}, "periodSeconds": 15, "timeoutSeconds": 5, "failureThreshold": 3}
]'

bind_custom_domain keycloak "$KEYCLOAK_CUSTOM_DOMAIN" "$KEYCLOAK_FQDN"

# Outbound email goes through Brevo's SMTP relay, configured entirely via .env
# (no Azure resource involved - see "Mail" in README-azure.md for why this
# replaced an earlier Azure Communication Services / Entra app approach).
if [[ -n "$BREVO_SMTP_LOGIN" && -n "$BREVO_SMTP_KEY" && -n "$MAIL_FROM_ADDRESS" ]]; then
  MAIL_CONFIGURED=true
else
  MAIL_CONFIGURED=false
  echo "!! BREVO_SMTP_LOGIN / BREVO_SMTP_KEY / MAIL_FROM_ADDRESS not all set in .env - the API and Keycloak will deploy without outbound email. See \"Mail\" in README-azure.md." >&2
fi

echo "==> Building the API image (version $BUDDY_VERSION)"
az acr build \
  --registry "$ACR_NAME" \
  --image buddy-api:latest \
  --build-arg "BUDDY_VERSION=$BUDDY_VERSION" \
  --build-arg "BUDDY_COMMIT=$BUDDY_COMMIT" \
  --file "$REPO_ROOT/src/backend/buddy/Dockerfile" \
  "$REPO_ROOT/src/backend"

API_DIGEST=$(az acr repository show --name "$ACR_NAME" --image buddy-api:latest --query digest -o tsv)
API_IMAGE="$ACR_SERVER/buddy-api@$API_DIGEST"

API_ENV_VARS=(
  ASPNETCORE_ENVIRONMENT=Production
  ConnectionStrings__Postgres=secretref:postgres-connection-string
  "Authentication__Keycloak__Authority=https://$KEYCLOAK_HOSTNAME/realms/buddy"
  "Authentication__Keycloak__ValidIssuer=https://$KEYCLOAK_HOSTNAME/realms/buddy"
  Authentication__Keycloak__Audience=buddy-api
  Authentication__Keycloak__RequireHttpsMetadata=true
  "Authentication__KeycloakAdmin__TokenEndpoint=https://$KEYCLOAK_HOSTNAME/realms/buddy/protocol/openid-connect/token"
  "Authentication__KeycloakAdmin__AdminBaseUrl=https://$KEYCLOAK_HOSTNAME/admin/realms/buddy"
  Authentication__KeycloakAdmin__ClientId=buddy-admin-cli
  Authentication__KeycloakAdmin__ClientSecret=secretref:keycloak-admin-cli-secret
  "Cors__AllowedOrigins__0=https://$FRONTEND_HOSTNAME"
  "Mail__FrontendBaseUrl=https://$FRONTEND_HOSTNAME"
  # Trust X-Forwarded-For from the Container Apps ingress (Envoy). Ingress is the only way in -- the
  # app has no public IP of its own -- and its source address range isn't fixed without a custom
  # VNet, so this trusts the private and shared (100.64/10) ranges rather than one subnet. Rate
  # limiting partitions anonymous callers by this address (docs/backend/analysis/rate-limiting.md).
  ForwardedHeaders__KnownNetworks__0=10.0.0.0/8
  ForwardedHeaders__KnownNetworks__1=172.16.0.0/12
  ForwardedHeaders__KnownNetworks__2=192.168.0.0/16
  ForwardedHeaders__KnownNetworks__3=100.64.0.0/10
)
if [[ "$MAIL_CONFIGURED" == true ]]; then
  API_ENV_VARS+=(
    "Mail__Host=$BREVO_SMTP_HOST"
    "Mail__Port=$BREVO_SMTP_PORT"
    Mail__UseSsl=false
    "Mail__Credentials__Username=$BREVO_SMTP_LOGIN"
    Mail__Credentials__Password=secretref:mail-smtp-password
    "Mail__FromAddress=$MAIL_FROM_ADDRESS"
    "Mail__FromName=$MAIL_FROM_NAME"
  )
fi

# Per-installation feature flags (docs/backend/analysis/feature-flags.md): FEATURES in .env is a
# comma-separated list of Name=true|false pairs. The API rejects an unknown name at startup, so the
# names are checked here too (case-insensitively, like the API's binding): a typo fails the deploy
# rather than leaving a crash-looping revision. Keep the list in step with FeatureOptions.cs.
KNOWN_FEATURES=" mealplans mealplanaiassistant mealplanimport medicines sleepdiary pickups babysitters worklocations printing progress tasklibrary help "
if [[ -n "${FEATURES:-}" ]]; then
  IFS=',' read -ra FEATURE_FLAGS <<< "$FEATURES"
  for flag in "${FEATURE_FLAGS[@]}"; do
    flag="${flag// /}"
    if [[ ! "$flag" =~ ^[A-Za-z]+=(true|false)$ ]]; then
      echo "FEATURES in .env: '$flag' is not Name=true or Name=false." >&2
      exit 1
    fi
    name="${flag%%=*}"
    if [[ "$KNOWN_FEATURES" != *" ${name,,} "* ]]; then
      echo "FEATURES in .env: '$name' is not a feature. Names: Mealplans, MealplanAiAssistant, MealplanImport, Medicines, SleepDiary, Pickups, Babysitters, WorkLocations, Printing, Progress, TaskLibrary, Help." >&2
      exit 1
    fi
    API_ENV_VARS+=("Features__$flag")
  done
fi

API_SECRETS=(
  "postgres-connection-string=Host=$PG_HOST;Port=5432;Database=$APP_DB_NAME;Username=$PG_ADMIN_USER;Password=$PG_ADMIN_PASSWORD;Ssl Mode=Require;Trust Server Certificate=true"
  "keycloak-admin-cli-secret=$KEYCLOAK_ADMIN_CLI_SECRET"
)
if [[ "$MAIL_CONFIGURED" == true ]]; then
  API_SECRETS+=("mail-smtp-password=$BREVO_SMTP_KEY")
fi

if containerapp_exists api; then
  echo "==> Updating API app config ($API_HOSTNAME)"
  az containerapp secret set \
    --name api \
    --resource-group "$RESOURCE_GROUP" \
    --secrets "${API_SECRETS[@]}" \
    -o none
  az containerapp update \
    --name api \
    --resource-group "$RESOURCE_GROUP" \
    --image "$API_IMAGE" \
    --cpu 1.0 --memory 2.0Gi \
    --set-env-vars "${API_ENV_VARS[@]}" \
    -o none
else
  echo "==> Deploying the API ($API_HOSTNAME)"
  az containerapp create \
    --name api \
    --resource-group "$RESOURCE_GROUP" \
    --environment "$CONTAINERAPPS_ENV" \
    --image "$API_IMAGE" \
    --registry-server "$ACR_SERVER" \
    --registry-username "$ACR_USERNAME" \
    --registry-password "$ACR_PASSWORD" \
    --target-port 8080 \
    --ingress external \
    --min-replicas 1 --max-replicas 3 \
    --cpu 1.0 --memory 2.0Gi \
    --secrets "${API_SECRETS[@]}" \
    --env-vars "${API_ENV_VARS[@]}" \
    -o none
fi

# /health and /health/ready (Common/Health/HealthChecksFeature.cs). The startup probe gives
# Marten's schema migrations up to 2 min before liveness takes over. A 503 from /health/ready
# (Postgres unreachable) takes the replica out of ingress without restarting it; a restart can't
# fix the database. Probes reach the container over plain HTTP with no X-Forwarded-Proto, so
# UseHttpsRedirection leaves them alone.
echo "==> Configuring the API's health probes"
set_probes api '[
  {"type": "Startup",   "httpGet": {"path": "/health",       "port": 8080}, "periodSeconds": 5,  "timeoutSeconds": 3, "failureThreshold": 24},
  {"type": "Liveness",  "httpGet": {"path": "/health",       "port": 8080}, "periodSeconds": 10, "timeoutSeconds": 3, "failureThreshold": 3},
  {"type": "Readiness", "httpGet": {"path": "/health/ready", "port": 8080}, "periodSeconds": 10, "timeoutSeconds": 5, "failureThreshold": 3}
]'

bind_custom_domain api "$API_CUSTOM_DOMAIN" "$API_FQDN"

echo "==> Building the frontend image (API_BASE_URL/KEYCLOAK_AUTHORITY baked in at build time)"
az acr build \
  --registry "$ACR_NAME" \
  --image buddy-frontend:latest \
  --build-arg "API_BASE_URL=https://$API_HOSTNAME" \
  --build-arg "KEYCLOAK_AUTHORITY=https://$KEYCLOAK_HOSTNAME" \
  --build-arg "BUDDY_VERSION=$BUDDY_VERSION" \
  --build-arg "REPOSITORY_URL=${REPOSITORY_URL:-}" \
  "$REPO_ROOT/src/frontend/buddy"

FRONTEND_DIGEST=$(az acr repository show --name "$ACR_NAME" --image buddy-frontend:latest --query digest -o tsv)
FRONTEND_IMAGE="$ACR_SERVER/buddy-frontend@$FRONTEND_DIGEST"

if containerapp_exists frontend; then
  echo "==> Updating frontend app ($FRONTEND_HOSTNAME)"
  az containerapp update \
    --name frontend \
    --resource-group "$RESOURCE_GROUP" \
    --image "$FRONTEND_IMAGE" \
    -o none
else
  echo "==> Deploying the frontend ($FRONTEND_HOSTNAME)"
  az containerapp create \
    --name frontend \
    --resource-group "$RESOURCE_GROUP" \
    --environment "$CONTAINERAPPS_ENV" \
    --image "$FRONTEND_IMAGE" \
    --registry-server "$ACR_SERVER" \
    --registry-username "$ACR_USERNAME" \
    --registry-password "$ACR_PASSWORD" \
    --target-port 80 \
    --ingress external \
    --min-replicas 1 --max-replicas 3 \
    --cpu 0.25 --memory 0.5Gi \
    -o none
fi

# Caddy serving static files: if / answers, the app is up. Nothing else to check.
echo "==> Configuring the frontend's health probes"
set_probes frontend '[
  {"type": "Liveness",  "httpGet": {"path": "/", "port": 80}, "periodSeconds": 30, "timeoutSeconds": 5, "failureThreshold": 3},
  {"type": "Readiness", "httpGet": {"path": "/", "port": 80}, "periodSeconds": 10, "timeoutSeconds": 5, "failureThreshold": 3}
]'

bind_custom_domain frontend "$FRONTEND_CUSTOM_DOMAIN" "$FRONTEND_FQDN"

if [[ "$MAIL_CONFIGURED" != true ]]; then
  echo "==> Skipping Keycloak SMTP configuration (outbound email isn't set up yet - see above)"
else
  # Keycloak sends its own mail (password resets, address verification, etc.)
  # and has no idea about the API's Mail__* config above - point its "buddy"
  # realm at the same Brevo SMTP credentials via the Admin REST API. The
  # realm itself is a manual, post-deploy step (README-azure.md, step 4), so
  # on a first run this just skips with a note to rerun once it exists.
  echo "==> Configuring the buddy realm's SMTP settings in Keycloak"
  KC_ADMIN_TOKEN=""
  if TOKEN_RESPONSE=$(curl -sf -X POST "https://$KEYCLOAK_HOSTNAME/realms/master/protocol/openid-connect/token" \
      -d "client_id=admin-cli" \
      -d "username=$KEYCLOAK_ADMIN" \
      -d "password=$KEYCLOAK_ADMIN_PASSWORD" \
      -d "grant_type=password" 2>/dev/null); then
    KC_ADMIN_TOKEN=$(echo "$TOKEN_RESPONSE" | jq -r .access_token)
  fi

  if [[ -z "$KC_ADMIN_TOKEN" || "$KC_ADMIN_TOKEN" == "null" ]]; then
    echo "!! Could not authenticate to Keycloak as $KEYCLOAK_ADMIN - skipping SMTP realm configuration. Rerun ./deploy.sh to retry." >&2
  elif ! CURRENT_REALM=$(curl -sf -H "Authorization: Bearer $KC_ADMIN_TOKEN" "https://$KEYCLOAK_HOSTNAME/admin/realms/buddy" 2>/dev/null); then
    echo "==> The buddy realm doesn't exist yet (see README-azure.md, step 4) - skipping SMTP configuration for now. Rerun ./deploy.sh once it's created."
  else
    UPDATED_REALM=$(echo "$CURRENT_REALM" | jq \
      --arg host "$BREVO_SMTP_HOST" \
      --arg port "$BREVO_SMTP_PORT" \
      --arg user "$BREVO_SMTP_LOGIN" \
      --arg password "$BREVO_SMTP_KEY" \
      --arg from "$MAIL_FROM_ADDRESS" \
      --arg fromName "$MAIL_FROM_NAME" \
      '.smtpServer = {host: $host, port: $port, from: $from, fromDisplayName: $fromName, ssl: "false", starttls: "true", auth: "true", user: $user, password: $password}')
    if curl -sf -X PUT "https://$KEYCLOAK_HOSTNAME/admin/realms/buddy" \
        -H "Authorization: Bearer $KC_ADMIN_TOKEN" \
        -H "Content-Type: application/json" \
        -d "$UPDATED_REALM" -o /dev/null; then
      echo "==> Keycloak's buddy realm now sends email through Brevo SMTP"
    else
      echo "!! Failed to update the buddy realm's SMTP settings. Rerun ./deploy.sh to retry." >&2
    fi
  fi
fi

cat <<EOF

==> Done.

  App:      https://$FRONTEND_HOSTNAME
  API:      https://$API_HOSTNAME
  Keycloak: https://$KEYCLOAK_HOSTNAME

EOF

if [[ -z "$FRONTEND_CUSTOM_DOMAIN$API_CUSTOM_DOMAIN$KEYCLOAK_CUSTOM_DOMAIN" ]]; then
  cat <<EOF
Next: configure the "buddy" realm (README-azure.md, step 4).

Whenever you're ready for your own domains instead of the ones above, fill
in FRONTEND_CUSTOM_DOMAIN / API_CUSTOM_DOMAIN / KEYCLOAK_CUSTOM_DOMAIN in
.env and rerun this script (README-azure.md, "Custom domains").
EOF
else
  echo 'Next: configure the "buddy" realm (README-azure.md, step 4).'
fi
