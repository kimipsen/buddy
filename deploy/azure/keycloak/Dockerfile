# syntax=docker/dockerfile:1

# Shared by both deployments:
#   - Azure:   az acr build --registry $ACR_NAME --image buddy-keycloak:latest \
#                --file deploy/azure/keycloak/Dockerfile .
#   - Compose: deploy/docker-compose.prod.yml builds it with HEALTH_ENABLED=true
# Build context is the repo root so themes/buddy is visible.
#
# `start --optimized` skips Keycloak's auto-build, so build-time options (the
# database vendor, health endpoints) only take effect if they're baked in here
# by `kc.sh build`. The stock image is built for `dev-file` (embedded H2).

FROM quay.io/keycloak/keycloak:26.8.0@sha256:b0f60d489d51c5d113390bdf5461d4c06e6051be026c05549f2e1e10ec352bcc

# Exposes /health, /health/ready, /health/live on the management port (9000). Off by default, so the Azure image is unchanged.
ARG HEALTH_ENABLED=false

COPY themes/buddy /opt/keycloak/themes/buddy

RUN /opt/keycloak/bin/kc.sh build --db=postgres --health-enabled="${HEALTH_ENABLED}"

ENTRYPOINT ["/opt/keycloak/bin/kc.sh"]
CMD ["start", "--optimized"]
