# syntax=docker/dockerfile:1

# Node's major version matches .nvmrc (which CI reads too); the digest pins the exact image.
FROM node:24-alpine@sha256:ebfe2f90462722a7a4de65e91990e97fe0d401c70e0e762c5b53302f905ec1c1 AS build
WORKDIR /app

COPY package.json package-lock.json .npmrc .nvmrc ./
RUN npm ci --ignore-scripts

COPY . .

# The development runtime config (localhost URLs) mustn't ship: the Caddyfile serves
# /config/runtime-config.json from the container's environment instead.
RUN npm run build -- --configuration production \
    && rm dist/buddy/browser/config/runtime-config.json

FROM caddy:2-alpine@sha256:d8542f48d34a9cf4e4c11a478865229840e87e4c96ea3f439101f31a5d35f75f AS runtime

# Caddy writes its state under /data and /config (XDG_DATA_HOME/XDG_CONFIG_HOME in the base
# image), so the non-root user must own them.
RUN if ! id -u caddy >/dev/null 2>&1; then \
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
