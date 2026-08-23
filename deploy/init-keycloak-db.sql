-- Runs once, on first init, via postgres's /docker-entrypoint-initdb.d hook.
-- POSTGRES_DB only creates the app database; Keycloak needs its own.
CREATE DATABASE keycloak;
