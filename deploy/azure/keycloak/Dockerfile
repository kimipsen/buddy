# syntax=docker/dockerfile:1

# Build context is the repo root so themes/buddy is visible.
# Build with: az acr build --registry $ACR_NAME --image buddy-keycloak:latest \
#   --file deploy/azure/keycloak/Dockerfile .

FROM quay.io/keycloak/keycloak:21.1.1

COPY themes/buddy /opt/keycloak/themes/buddy

RUN /opt/keycloak/bin/kc.sh build --db=postgres

ENTRYPOINT ["/opt/keycloak/bin/kc.sh"]
CMD ["start", "--optimized"]
