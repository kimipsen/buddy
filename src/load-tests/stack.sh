#!/usr/bin/env bash
# Starts or stops the isolated load-test stack (task load:stack:up / load:stack:down).
#
#   stack.sh up    build buddy-api:load from the working tree (or pull BUDDY_API_IMAGE), start
#                  everything and wait until the API's /health/ready answers 200
#   stack.sh down  stop it and delete its volume, which is the whole cleanup
set -euo pipefail

cd "$(dirname "$0")"
COMPOSE=(docker compose -f docker-compose.load.yml)

case "${1:-}" in
  up)
    if [ -n "${BUDDY_API_IMAGE:-}" ]; then
      docker pull "$BUDDY_API_IMAGE"
    else
      # A plain build rather than compose's: host networking for the NuGet restore, because the
      # devcontainer's nested Docker has no DNS on bridge networks (harmless on CI runners).
      docker build --network host -t buddy-api:load -f ../backend/buddy/Dockerfile ../backend
    fi
    "${COMPOSE[@]}" up -d --wait
    echo "Load stack ready: API http://localhost:5280, Grafana http://localhost:3300"
    ;;
  down)
    "${COMPOSE[@]}" --profile k6 down -v
    ;;
  *)
    echo "usage: stack.sh up|down" >&2
    exit 2
    ;;
esac
