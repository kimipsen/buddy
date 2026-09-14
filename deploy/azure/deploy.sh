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

# shellcheck disable=SC1091
source "$SCRIPT_DIR/.env"

# Optional - blank means "use the Azure-assigned default domain".
: "${FRONTEND_CUSTOM_DOMAIN:=}"
: "${API_CUSTOM_DOMAIN:=}"
: "${KEYCLOAK_CUSTOM_DOMAIN:=}"

# Optional - defaults let existing .env files keep working unmodified.
: "${COMM_SERVICE_NAME:=buddy-comm}"
: "${EMAIL_SERVICE_NAME:=buddy-email}"
: "${COMM_DATA_LOCATION:=Europe}"
: "${MAIL_SMTP_USERNAME:=buddy-smtp}"

# Optional - set both if your account can't create Entra app registrations
# itself (see "Mail" in README-azure.md) and someone with rights created one
# for you instead. Blank means deploy.sh creates its own.
: "${SMTP_APP_ID:=}"
: "${SMTP_APP_SECRET:=}"

SMTP_APP_NAME="$COMM_SERVICE_NAME-smtp"

containerapp_exists() {
  az containerapp show --name "$1" --resource-group "$RESOURCE_GROUP" -o none 2>/dev/null
}

containerapp_env_exists() {
  az containerapp env show --name "$CONTAINERAPPS_ENV" --resource-group "$RESOURCE_GROUP" -o none 2>/dev/null
}

postgres_server_exists() {
  az postgres flexible-server show --name "$PG_SERVER_NAME" --resource-group "$RESOURCE_GROUP" -o none 2>/dev/null
}

postgres_firewall_rule_exists() {
  az postgres flexible-server firewall-rule show --resource-group "$RESOURCE_GROUP" \
    --server-name "$PG_SERVER_NAME" --name "$1" -o none 2>/dev/null
}

postgres_db_exists() {
  az postgres flexible-server db show --resource-group "$RESOURCE_GROUP" \
    --server-name "$PG_SERVER_NAME" --name "$1" -o none 2>/dev/null
}

email_service_exists() {
  az communication email show --name "$EMAIL_SERVICE_NAME" --resource-group "$RESOURCE_GROUP" -o none 2>/dev/null
}

email_domain_exists() {
  az communication email domain show --domain-name AzureManagedDomain \
    --email-service-name "$EMAIL_SERVICE_NAME" --resource-group "$RESOURCE_GROUP" -o none 2>/dev/null
}

communication_service_exists() {
  az communication show --name "$COMM_SERVICE_NAME" --resource-group "$RESOURCE_GROUP" -o none 2>/dev/null
}

smtp_app_id() {
  az ad app list --display-name "$SMTP_APP_NAME" --query "[0].appId" -o tsv 2>/dev/null
}

smtp_username_exists() {
  az communication smtp-username show --comm-service-name "$COMM_SERVICE_NAME" \
    --resource-group "$RESOURCE_GROUP" --smtp-username "$MAIL_SMTP_USERNAME" -o none 2>/dev/null
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

echo "==> Registering resource providers and the containerapp/communication CLI extensions"
az extension add --name containerapp --upgrade -o none -y
az extension add --name communication --upgrade -o none -y
az provider register --namespace Microsoft.App -o none
az provider register --namespace Microsoft.OperationalInsights -o none
az provider register --namespace Microsoft.DBforPostgreSQL -o none
az provider register --namespace Microsoft.ContainerRegistry -o none
az provider register --namespace Microsoft.Communication -o none

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

# Public network access has to be Enabled for firewall rules to apply at all
# (safe/idempotent to (re)run even if it's already enabled). Access is then
# restricted down to just Azure services via the firewall rule below -
# tighten to a VNet-integrated / private endpoint setup later if you want to
# remove public network exposure entirely.
echo "==> Ensuring the Postgres server allows public network access"
az postgres flexible-server update \
  --resource-group "$RESOURCE_GROUP" \
  --name "$PG_SERVER_NAME" \
  --public-access Enabled \
  -o none

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
  "KC_HOSTNAME=$KEYCLOAK_HOSTNAME"
  KC_HTTP_ENABLED=true
  KC_PROXY=edge
  "KEYCLOAK_ADMIN=$KEYCLOAK_ADMIN"
  KEYCLOAK_ADMIN_PASSWORD=secretref:keycloak-admin-password
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

bind_custom_domain keycloak "$KEYCLOAK_CUSTOM_DOMAIN" "$KEYCLOAK_FQDN"

echo "==> Creating the Email Communication Service $EMAIL_SERVICE_NAME"
if email_service_exists; then
  echo "==> Email Communication Service $EMAIL_SERVICE_NAME already exists, skipping creation"
else
  az communication email create \
    --name "$EMAIL_SERVICE_NAME" \
    --resource-group "$RESOURCE_GROUP" \
    --location "Global" \
    --data-location "$COMM_DATA_LOCATION" \
    -o none
fi

# Azure Managed Domain: a free, ready-to-send *.azurecomm.net domain with no DNS
# verification step, at the cost of a fixed "DoNotReply" sender and a low sending
# quota. Good enough to get real mail flowing; switch to a verified custom domain
# (--domain-management CustomerManaged) later for your own sender address and
# production-grade volume - see the Mail section in README-azure.md.
echo "==> Creating the Azure managed email domain"
if email_domain_exists; then
  echo "==> Email domain already exists, skipping creation"
else
  az communication email domain create \
    --domain-name AzureManagedDomain \
    --email-service-name "$EMAIL_SERVICE_NAME" \
    --resource-group "$RESOURCE_GROUP" \
    --location "Global" \
    --domain-management AzureManaged \
    -o none
fi

EMAIL_DOMAIN_ID=$(az communication email domain show \
  --domain-name AzureManagedDomain \
  --email-service-name "$EMAIL_SERVICE_NAME" \
  --resource-group "$RESOURCE_GROUP" \
  --query id -o tsv)

MAIL_FROM_DOMAIN=$(az communication email domain show \
  --domain-name AzureManagedDomain \
  --email-service-name "$EMAIL_SERVICE_NAME" \
  --resource-group "$RESOURCE_GROUP" \
  --query properties.mailFromSenderDomain -o tsv)

echo "==> Creating the Communication Services resource $COMM_SERVICE_NAME"
if communication_service_exists; then
  echo "==> Communication Services resource $COMM_SERVICE_NAME already exists, skipping creation"
  az communication update \
    --name "$COMM_SERVICE_NAME" \
    --resource-group "$RESOURCE_GROUP" \
    --linked-domains "$EMAIL_DOMAIN_ID" \
    -o none
else
  az communication create \
    --name "$COMM_SERVICE_NAME" \
    --resource-group "$RESOURCE_GROUP" \
    --location "Global" \
    --data-location "$COMM_DATA_LOCATION" \
    --linked-domains "$EMAIL_DOMAIN_ID" \
    -o none
fi

COMM_SERVICE_ID=$(az communication show --name "$COMM_SERVICE_NAME" --resource-group "$RESOURCE_GROUP" --query id -o tsv)

# SMTP AUTH for Communication Services Email is backed by an Entra app + client
# secret, not a static password - each credential-minting command below prints
# its secret exactly once, so it only runs when there's no usable SMTP username
# yet. A rerun after a mid-setup failure re-plugs into whichever step didn't
# finish (existing app -> mint a fresh secret for it; existing SMTP username ->
# skip entirely) rather than starting over or getting stuck.
#
# None of this is allowed to abort the deploy: creating an Entra app
# registration needs a directory permission Contributor alone doesn't grant,
# and plenty of org tenants don't hand that out by default. If it's missing,
# everything below is skipped and the API/Keycloak just deploy without
# outbound email - see the printed guidance and "Mail" in README-azure.md.
MAIL_SMTP_PASSWORD=""
if smtp_username_exists; then
  echo "==> SMTP username $MAIL_SMTP_USERNAME already exists, skipping SMTP credential creation"
elif [[ -n "$SMTP_APP_ID" && -n "$SMTP_APP_SECRET" ]]; then
  echo "==> Using the SMTP_APP_ID from .env (created for you by someone with Entra app registration rights)"
  MAIL_SMTP_PASSWORD="$SMTP_APP_SECRET"
  SMTP_TENANT_ID=$(az account show --query tenantId -o tsv)
else
  echo "==> Creating the Entra app for SMTP AUTH"
  EXISTING_SMTP_APP_ID=$(smtp_app_id)
  if [[ -n "$EXISTING_SMTP_APP_ID" ]]; then
    SMTP_APP_ID="$EXISTING_SMTP_APP_ID"
    SMTP_TENANT_ID=$(az account show --query tenantId -o tsv)
    if ! MAIL_SMTP_PASSWORD=$(az ad app credential reset --id "$SMTP_APP_ID" --query password -o tsv 2>/tmp/smtp-app-error); then
      cat /tmp/smtp-app-error >&2
      echo "!! Could not mint a new client secret for the existing $SMTP_APP_NAME Entra app - skipping SMTP setup for this run. See \"Mail\" in README-azure.md." >&2
      MAIL_SMTP_PASSWORD=""
    fi
  elif ! SP_OUTPUT=$(az ad sp create-for-rbac \
      --name "$SMTP_APP_NAME" \
      --role "Communication and Email Service Owner" \
      --scopes "$COMM_SERVICE_ID" \
      --query "[appId, password, tenant]" -o tsv 2>/tmp/smtp-app-error); then
    cat /tmp/smtp-app-error >&2
    cat <<WARN >&2
!! Could not create the Entra app registration needed for SMTP AUTH - your
   account likely lacks permission to register applications in this tenant
   (common in managed orgs). The rest of the deploy will continue without
   outbound email. To finish this later, either:
     - ask your Entra/Azure admin to grant you the "Application Developer"
       Entra role (or enable "Users can register applications"), then rerun
       ./deploy.sh, or
     - ask them to run this and give you back the appId/password:
         az ad sp create-for-rbac --name "$SMTP_APP_NAME" \\
           --role "Communication and Email Service Owner" \\
           --scopes "$COMM_SERVICE_ID"
       then set SMTP_APP_ID / SMTP_APP_SECRET in .env to those values and
       rerun ./deploy.sh.
   See "Mail" in README-azure.md for details.
WARN
  else
    IFS=$'\t' read -r SMTP_APP_ID MAIL_SMTP_PASSWORD SMTP_TENANT_ID <<<"$SP_OUTPUT"
  fi
fi

# Whichever path above produced a password (existing username, .env override,
# reset secret, or a freshly created app), turn it into the actual SMTP
# username Keycloak/the API authenticate with. The role assignment behind a
# freshly created app can take up to a couple of minutes to propagate, so
# retry until smtp-username create sees it rather than failing immediately.
if [[ -n "$MAIL_SMTP_PASSWORD" ]] && ! smtp_username_exists; then
  echo "==> Creating the SMTP username (waiting for the role assignment to propagate if needed)"
  for attempt in $(seq 1 10); do
    if az communication smtp-username create \
        --comm-service-name "$COMM_SERVICE_NAME" \
        --resource-group "$RESOURCE_GROUP" \
        --smtp-username "$MAIL_SMTP_USERNAME" \
        --username "$MAIL_SMTP_USERNAME" \
        --entra-application-id "$SMTP_APP_ID" \
        --tenant-id "$SMTP_TENANT_ID" \
        -o none 2>/dev/null; then
      break
    fi
    if [[ "$attempt" -eq 10 ]]; then
      echo "!! Timed out waiting for the role assignment to propagate. Rerun ./deploy.sh to retry - it will pick up where this left off." >&2
      MAIL_SMTP_PASSWORD=""
    fi
    sleep 15
  done
fi

# The source of truth for whether SMTP is actually usable is Azure's state,
# not whether the steps above happened to run (or succeed) this time.
if smtp_username_exists; then
  MAIL_CONFIGURED=true
else
  MAIL_CONFIGURED=false
  echo "!! Outbound email is not configured - the API and Keycloak will deploy without it. See \"Mail\" in README-azure.md to finish setup later." >&2
fi

echo "==> Building the API image"
az acr build \
  --registry "$ACR_NAME" \
  --image buddy-api:latest \
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
  "Authentication__KeycloakAdmin__TokenEndpoint=https://$KEYCLOAK_HOSTNAME/realms/master/protocol/openid-connect/token"
  "Authentication__KeycloakAdmin__AdminBaseUrl=https://$KEYCLOAK_HOSTNAME/admin/realms/buddy"
  Authentication__KeycloakAdmin__ClientId=buddy-admin-cli
  Authentication__KeycloakAdmin__ClientSecret=secretref:keycloak-admin-cli-secret
  "Cors__AllowedOrigins__0=https://$FRONTEND_HOSTNAME"
  "Mail__FrontendBaseUrl=https://$FRONTEND_HOSTNAME"
)
if [[ "$MAIL_CONFIGURED" == true ]]; then
  API_ENV_VARS+=(
    Mail__Host=smtp.azurecomm.net
    Mail__Port=587
    Mail__UseSsl=false
    "Mail__Username=$MAIL_SMTP_USERNAME"
    Mail__Password=secretref:mail-smtp-password
    "Mail__FromAddress=DoNotReply@$MAIL_FROM_DOMAIN"
  )
fi

API_SECRETS=(
  "postgres-connection-string=Host=$PG_HOST;Port=5432;Database=$APP_DB_NAME;Username=$PG_ADMIN_USER;Password=$PG_ADMIN_PASSWORD;Ssl Mode=Require;Trust Server Certificate=true"
  "keycloak-admin-cli-secret=$KEYCLOAK_ADMIN_CLI_SECRET"
)
if [[ -n "$MAIL_SMTP_PASSWORD" ]]; then
  API_SECRETS+=("mail-smtp-password=$MAIL_SMTP_PASSWORD")
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

bind_custom_domain api "$API_CUSTOM_DOMAIN" "$API_FQDN"

echo "==> Building the frontend image (API_BASE_URL/KEYCLOAK_AUTHORITY baked in at build time)"
az acr build \
  --registry "$ACR_NAME" \
  --image buddy-frontend:latest \
  --build-arg "API_BASE_URL=https://$API_HOSTNAME" \
  --build-arg "KEYCLOAK_AUTHORITY=https://$KEYCLOAK_HOSTNAME" \
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

bind_custom_domain frontend "$FRONTEND_CUSTOM_DOMAIN" "$FRONTEND_FQDN"

if [[ "$MAIL_CONFIGURED" != true ]]; then
  echo "==> Skipping Keycloak SMTP configuration (outbound email isn't set up yet - see above)"
else
  # Keycloak sends its own mail (password resets, address verification, etc.)
  # and has no idea about the ACS setup above - point its "buddy" realm at the
  # same SMTP credentials via the Admin REST API. The realm itself is a
  # manual, post-deploy step (README-azure.md, step 4), so on a first run
  # this just skips with a note to rerun once it exists.
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
    MAIL_SMTP_PASSWORD_VALUE=$(az containerapp secret list --name api --resource-group "$RESOURCE_GROUP" --show-values \
      --query "[?name=='mail-smtp-password'].value | [0]" -o tsv)
    UPDATED_REALM=$(echo "$CURRENT_REALM" | jq \
      --arg user "$MAIL_SMTP_USERNAME" \
      --arg password "$MAIL_SMTP_PASSWORD_VALUE" \
      --arg from "DoNotReply@$MAIL_FROM_DOMAIN" \
      '.smtpServer = {host: "smtp.azurecomm.net", port: "587", from: $from, ssl: "false", starttls: "true", auth: "true", user: $user, password: $password}')
    if curl -sf -X PUT "https://$KEYCLOAK_HOSTNAME/admin/realms/buddy" \
        -H "Authorization: Bearer $KC_ADMIN_TOKEN" \
        -H "Content-Type: application/json" \
        -d "$UPDATED_REALM" -o /dev/null; then
      echo "==> Keycloak's buddy realm now sends email through $EMAIL_SERVICE_NAME"
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
