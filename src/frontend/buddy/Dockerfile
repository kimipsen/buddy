# syntax=docker/dockerfile:1

FROM node:26-alpine AS build
WORKDIR /app

COPY package.json package-lock.json ./
RUN npm ci --ignore-scripts --legacy-peer-deps

COPY . .

ARG API_BASE_URL
ARG KEYCLOAK_AUTHORITY
# From deploy/version.sh (the build context has no .git); shown in the profile menu.
ARG BUDDY_VERSION=0.0.0-dev
RUN npm run build -- --configuration production

# Runtime config is fetched by the app at startup (see runtime-config.service.ts),
# not baked into the JS bundle — overwrite it here so a rebuild picks up the
# right domains without touching app source.
RUN node -e " \
  const fs = require('fs'); \
  const path = 'dist/buddy/browser/config/runtime-config.json'; \
  const config = JSON.parse(fs.readFileSync(path, 'utf8')); \
  config.keycloak.authority = process.env.KEYCLOAK_AUTHORITY; \
  config.apiBaseUrl = process.env.API_BASE_URL; \
  config.version = process.env.BUDDY_VERSION; \
  fs.writeFileSync(path, JSON.stringify(config, null, 2)); \
"

FROM caddy:2-alpine AS runtime

# Caddy writes its state under /data and /config (XDG_DATA_HOME/XDG_CONFIG_HOME in the base
# image), so the non-root user must own them.
RUN if ! id -u caddy >/dev/null 2>&1; then \
      addgroup -S caddy && adduser -S -G caddy caddy; \
    fi \
    && chown -R caddy:caddy /data /config

# The Caddyfile reads these for the Content-Security-Policy's connect-src/form-action, so the
# policy names the same API and Keycloak origins as runtime-config.json.
ARG API_BASE_URL
ARG KEYCLOAK_AUTHORITY
ENV API_BASE_URL=$API_BASE_URL \
    KEYCLOAK_AUTHORITY=$KEYCLOAK_AUTHORITY

COPY --from=build /app/dist/buddy/browser /usr/share/caddy
COPY Caddyfile /etc/caddy/Caddyfile

# The caddy binary carries cap_net_bind_service in the base image, so :80 still binds.
USER caddy
