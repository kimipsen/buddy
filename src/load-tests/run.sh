#!/usr/bin/env bash
# Runs one k6 profile (task test:load PROFILE=… TARGET=…). See docs/backend/analysis/load-testing-k6.md.
#
#   TARGET=stack (default)  the isolated buddy-load Compose stack (task load:stack:up first)
#   TARGET=dev              the devcontainer's running API and Keycloak; smoke only
#
# Writes results/<profile>-<timestamp>/{summary.json,report.html}. Exits non-zero when k6's
# thresholds fail, or (stack only) when the API logged 53300 (too many clients) or its container
# restarted or was OOM-killed during the run, the checks k6 itself can't make.
set -euo pipefail

cd "$(dirname "$0")"

PROFILE="${PROFILE:-smoke}"
TARGET="${TARGET:-stack}"
# Keep in step with the k6 service in docker-compose.load.yml.
K6_IMAGE="grafana/k6:2.3.0@sha256:9c2dee7f8ed74d317e4027c06a10f169b625638189de8d4555d0b3486a5aeb34"
COMPOSE=(docker compose -f docker-compose.load.yml)

if [ ! -f "scenarios/${PROFILE}.ts" ]; then
  echo "Unknown PROFILE '${PROFILE}'. One of: $(cd scenarios && ls ./*.ts | sed 's|./||; s|\.ts||' | tr '\n' ' ')" >&2
  exit 2
fi

# On the load stack, removing the seeded families only costs teardown time: stack.sh down deletes
# the volume anyway. On the shared dev stack, teardown always cleans up.
if [ "$TARGET" = "stack" ]; then
  CLEANUP="${CLEANUP:-0}"
fi

RESULTS="results/${PROFILE}-$(date -u +%Y%m%dT%H%M%SZ)"
mkdir -p "$RESULTS"

# The tuning knobs the scenarios read from __ENV; passed through when set.
PASS_THROUGH=(FAMILIES VUS HOLD STEPS RATE_STEP MINUTES MAX_VUS DURATION RECOVERY_AT SEED_HISTORY_DAYS THINK_TIME CLEANUP RUN_ID)
ENV_ARGS=()
for name in "${PASS_THROUGH[@]}"; do
  if [ -n "${!name:-}" ]; then
    ENV_ARGS+=(-e "$name=${!name}")
  fi
done
ENV_ARGS+=(-e "K6_WEB_DASHBOARD=true" -e "K6_WEB_DASHBOARD_EXPORT=${RESULTS}/report.html")
K6_ARGS=(run --summary-trend-stats "avg,min,med,max,p(90),p(95),p(99)" --summary-export "${RESULTS}/summary.json" "scenarios/${PROFILE}.ts")

if [ "$TARGET" = "dev" ]; then
  if [ "$PROFILE" != "smoke" ]; then
    echo "TARGET=dev runs smoke only: every worktree shares the dev database and Keycloak. Use the load stack." >&2
    exit 2
  fi

  # Host networking reaches the dev API on localhost and Keycloak by its devcontainer hostname.
  set +e
  docker run --rm --network host -u "$(id -u)" -v "$PWD:/scripts" -w /scripts \
    -e "BASE_URL=${BASE_URL:-http://localhost:5193}" \
    -e "KEYCLOAK_URL=${KEYCLOAK_URL:-http://keycloak:8080}" \
    -e "KEYCLOAK_ADMIN_USER=${KEYCLOAK_ADMIN:-admin}" \
    -e "KEYCLOAK_ADMIN_PASSWORD=${KEYCLOAK_ADMIN_PASSWORD:-admin}" \
    -e K6_NO_USAGE_REPORT=true \
    "${ENV_ARGS[@]}" "$K6_IMAGE" "${K6_ARGS[@]}"
  status=$?
  set -e
  echo "Results: src/load-tests/${RESULTS}"
  exit $status
fi

api_container() { "${COMPOSE[@]}" ps -q api; }

if [ -z "$(api_container)" ]; then
  echo "The load stack isn't running. Start it with: task load:stack:up" >&2
  exit 2
fi

# breakpoint measures the stack's capacity, so the per-user limiter must not end it first. Exported
# for the whole run: any compose command without them would recreate the API with the defaults.
if [ "$PROFILE" = "breakpoint" ]; then
  export LOAD_AUTHENTICATED_TOKEN_LIMIT=1000000 LOAD_AUTHENTICATED_TOKENS_PER_PERIOD=100000
  "${COMPOSE[@]}" up -d --wait --no-build api
fi

before="$(docker inspect -f '{{.RestartCount}} {{.State.StartedAt}}' "$(api_container)")"
since="$(date -u +%Y-%m-%dT%H:%M:%SZ)"

set +e
# --no-deps: the stack is already up (checked above), and compose must not recreate the API
# mid-run because it thinks the API's configuration drifted.
LOAD_UID="$(id -u)" "${COMPOSE[@]}" --profile k6 run --rm --no-deps "${ENV_ARGS[@]}" k6 "${K6_ARGS[@]}" --out opentelemetry
status=$?
set -e

if [ "$PROFILE" = "breakpoint" ]; then
  unset LOAD_AUTHENTICATED_TOKEN_LIMIT LOAD_AUTHENTICATED_TOKENS_PER_PERIOD
  "${COMPOSE[@]}" up -d --wait --no-build api
else
  after="$(docker inspect -f '{{.RestartCount}} {{.State.StartedAt}}' "$(api_container)")"
  if [ "$before" != "$after" ] || [ "$(docker inspect -f '{{.State.OOMKilled}}' "$(api_container)")" = "true" ]; then
    echo "FAIL: the API container restarted or was OOM-killed during the run (${before} -> ${after})." >&2
    status=1
  fi
fi

if "${COMPOSE[@]}" logs --since "$since" api 2>&1 | grep -q '53300'; then
  echo "FAIL: the API logged 53300 (too many clients): the Postgres pool ran out." >&2
  status=1
fi

echo "Results: src/load-tests/${RESULTS}  (Grafana: http://localhost:3300)"
exit $status
