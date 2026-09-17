#!/bin/sh
set -eu

# Runs once, only when the Postgres data volume is first initialized (see
# docker-entrypoint-initdb.d in the official postgres image). Keycloak is
# configured to use a separate database named "keycloak" (KC_DB_URL_DATABASE
# in docker-compose.yml), distinct from POSTGRES_DB.
psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname postgres <<-EOSQL
    CREATE DATABASE keycloak;
EOSQL

# KC_DB_USERNAME/KC_DB_PASSWORD (see .env.example) are commonly set to reuse
# POSTGRES_USER/POSTGRES_PASSWORD, in which case the superuser above already
# owns "keycloak" and there's nothing left to do. If a deployment instead
# gives Keycloak its own dedicated role, create it here too -- otherwise
# Keycloak can create the database but can never log in to use it.
if [ -n "${KC_DB_USERNAME:-}" ] && [ "$KC_DB_USERNAME" != "$POSTGRES_USER" ]; then
    psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname postgres \
        -v role="$KC_DB_USERNAME" -v password="$KC_DB_PASSWORD" <<-'EOSQL'
        CREATE ROLE :"role" WITH LOGIN PASSWORD :'password';
        ALTER DATABASE keycloak OWNER TO :"role";
EOSQL
fi
