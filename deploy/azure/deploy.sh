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

# Outbound email (Gmail SMTP) - optional, blank means the API/Keycloak deploy
# without it. See "Mail" in README-azure.md for how to set up an App Password.
: "${GMAIL_SMTP_USER:=}"
: "${GMAIL_SMTP_APP_PASSWORD:=}"

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

# Outbound email goes through Gmail SMTP, configured entirely via .env (no
# Azure resource involved - see "Mail" in README-azure.md for why this
# replaced an earlier Azure Communication Services / Entra app approach).
if [[ -n "$GMAIL_SMTP_USER" && -n "$GMAIL_SMTP_APP_PASSWORD" ]]; then
  MAIL_CONFIGURED=true
else
  MAIL_CONFIGURED=false
  echo "!! GMAIL_SMTP_USER / GMAIL_SMTP_APP_PASSWORD not set in .env - the API and Keycloak will deploy without outbound email. See \"Mail\" in README-azure.md." >&2
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
  "Authentication__KeycloakAdmin__TokenEndpoint=https://$KEYCLOAK_HOSTNAME/realms/master/protocol/openid-connect/token"
  "Authentication__KeycloakAdmin__AdminBaseUrl=https://$KEYCLOAK_HOSTNAME/admin/realms/buddy"
  Authentication__KeycloakAdmin__ClientId=buddy-admin-cli
  Authentication__KeycloakAdmin__ClientSecret=secretref:keycloak-admin-cli-secret
  "Cors__AllowedOrigins__0=https://$FRONTEND_HOSTNAME"
  "Mail__FrontendBaseUrl=https://$FRONTEND_HOSTNAME"
)
if [[ "$MAIL_CONFIGURED" == true ]]; then
  API_ENV_VARS+=(
    Mail__Host=smtp.gmail.com
    Mail__Port=587
    Mail__UseSsl=false
    "Mail__Credentials__Username=$GMAIL_SMTP_USER"
    Mail__Credentials__Password=secretref:mail-smtp-password
    "Mail__FromAddress=$GMAIL_SMTP_USER"
  )
fi

API_SECRETS=(
  "postgres-connection-string=Host=$PG_HOST;Port=5432;Database=$APP_DB_NAME;Username=$PG_ADMIN_USER;Password=$PG_ADMIN_PASSWORD;Ssl Mode=Require;Trust Server Certificate=true"
  "keycloak-admin-cli-secret=$KEYCLOAK_ADMIN_CLI_SECRET"
)
if [[ "$MAIL_CONFIGURED" == true ]]; then
  API_SECRETS+=("mail-smtp-password=$GMAIL_SMTP_APP_PASSWORD")
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
  --build-arg "BUDDY_VERSION=$BUDDY_VERSION" \
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
  # and has no idea about the API's Mail__* config above - point its "buddy"
  # realm at the same Gmail SMTP credentials via the Admin REST API. The
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
      --arg user "$GMAIL_SMTP_USER" \
      --arg password "$GMAIL_SMTP_APP_PASSWORD" \
      --arg from "$GMAIL_SMTP_USER" \
      '.smtpServer = {host: "smtp.gmail.com", port: "587", from: $from, ssl: "false", starttls: "true", auth: "true", user: $user, password: $password}')
    if curl -sf -X PUT "https://$KEYCLOAK_HOSTNAME/admin/realms/buddy" \
        -H "Authorization: Bearer $KC_ADMIN_TOKEN" \
        -H "Content-Type: application/json" \
        -d "$UPDATED_REALM" -o /dev/null; then
      echo "==> Keycloak's buddy realm now sends email through Gmail SMTP"
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
