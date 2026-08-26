# syntax=docker/dockerfile:1

FROM node:26-alpine AS build
WORKDIR /app

COPY package.json package-lock.json ./
RUN npm ci

COPY . .

ARG API_BASE_URL
ARG KEYCLOAK_AUTHORITY
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
  fs.writeFileSync(path, JSON.stringify(config, null, 2)); \
"

FROM caddy:2-alpine AS runtime
COPY --from=build /app/dist/buddy/browser /usr/share/caddy
COPY Caddyfile /etc/caddy/Caddyfile
