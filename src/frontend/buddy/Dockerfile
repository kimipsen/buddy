# syntax=docker/dockerfile:1

# Node's major version matches .nvmrc (which CI reads too).
FROM node:24-alpine AS build
WORKDIR /app

COPY package.json package-lock.json .npmrc .nvmrc ./
RUN npm ci --ignore-scripts

COPY . .

# The development runtime config (localhost URLs) mustn't ship: the Caddyfile serves
# /config/runtime-config.json from the container's environment instead.
RUN npm run build -- --configuration production \
    && rm dist/buddy/browser/config/runtime-config.json

FROM caddy:2-alpine AS runtime

# Alpine publishes package fixes (e.g. brotli-libs 1.2.1 for CVE-2026-111497) before the caddy
# image is rebuilt, so the upgrade picks up what the Trivy gate reports as fixable.
# Nothing here uses curl (the healthcheck is busybox wget), so it goes, and its CVEs with it.
# Caddy writes its state under /data and /config (XDG_DATA_HOME/XDG_CONFIG_HOME in the base
# image), so the non-root user must own them.
RUN apk upgrade --no-cache \
    && apk del --no-cache curl \
    && if ! id -u caddy >/dev/null 2>&1; then \
      addgroup -S caddy && adduser -S -G caddy caddy; \
    fi \
    && chown -R caddy:caddy /data /config

# One image serves any deployment: API_BASE_URL, KEYCLOAK_AUTHORITY and the optional
# REPOSITORY_URL come from the container's environment at start, where the Caddyfile writes them
# into runtime-config.json and the Content-Security-Policy. Only the version is baked in, because
# it belongs to the image: from deploy/version.sh (the build context has no .git), shown in the
# profile menu.
ARG BUDDY_VERSION=0.0.0-dev
ENV BUDDY_VERSION=$BUDDY_VERSION

COPY --from=build /app/dist/buddy/browser /usr/share/caddy
COPY Caddyfile /etc/caddy/Caddyfile

# The caddy binary carries cap_net_bind_service in the base image, so :80 still binds.
USER caddy
