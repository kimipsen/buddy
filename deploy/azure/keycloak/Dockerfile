# syntax=docker/dockerfile:1

# Shared by both deployments:
#   - Azure:   az acr build --registry $ACR_NAME --image buddy-keycloak:latest \
#                --build-arg HEALTH_ENABLED=true --file deploy/azure/keycloak/Dockerfile .
#   - Compose: deploy/docker-compose.prod.yml builds it with HEALTH_ENABLED=true
# Build context is the repo root so themes/buddy is visible.
#
# `start --optimized` skips Keycloak's auto-build, so build-time options (the
# database vendor, health endpoints) only take effect if they're baked in here
# by `kc.sh build`. The stock image is built for `dev-file` (embedded H2).

FROM quay.io/keycloak/keycloak:26.8.1@sha256:3f3c5f8e03c614774b4bd9173b88391fefa4153bfc010d8012d59ad825377332

# Exposes /health, /health/started, /health/ready, /health/live on the management port (9000). Off by default; both deployments turn it on for their probes.
ARG HEALTH_ENABLED=false

COPY themes/buddy /opt/keycloak/themes/buddy

RUN /opt/keycloak/bin/kc.sh build --db=postgres --health-enabled="${HEALTH_ENABLED}"

ENTRYPOINT ["/opt/keycloak/bin/kc.sh"]
CMD ["start", "--optimized"]
