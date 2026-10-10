#!/usr/bin/env bash
# Guard for `task deploy` (docker-compose.prod.yml). Run from anywhere.
#
# `docker compose up` acts on whatever Docker daemon the current shell talks
# to; there is no remote host in the setup. Run from the devcontainer or a
# laptop, it would start the production stack locally against deploy/.env and
# have Caddy request Let's Encrypt certificates for the real domains. So this
# refuses to continue unless:
#
#   1. we're not inside a container (devcontainer, Codespaces, `docker exec`
#      shell): REMOTE_CONTAINERS / CODESPACES are unset and /.dockerenv is
#      absent. The deploy is meant to run on the VM host itself.
#   2. DEPLOY_HOST in deploy/.env equals this machine's `hostname`. This is a
#      positive identification of the VM: a copy of the .env on another machine
#      (or a fresh one from .env.example) fails it.
#
# It also refuses first-boot placeholder passwords from .env.example and warns
# (without failing) when KEYCLOAK_ADMIN_CLI_SECRET is still the placeholder, or
# when the commit being deployed is missing the newest release (release-check.sh).
# Values are compared in memory and never printed (DEPLOY_HOST excepted).
# There is deliberately no override flag: to deploy to a different machine,
# change DEPLOY_HOST in that machine's .env.
set -euo pipefail

dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
env_file="$dir/.env"
example="$dir/.env.example"

fail() { echo "deploy preflight: $*" >&2; exit 1; }

[[ -f "$env_file" ]] || fail "deploy/.env not found. Copy deploy/.env.example to deploy/.env and fill in real values first."

if [[ -n "${REMOTE_CONTAINERS:-}" || -n "${CODESPACES:-}" || -f /.dockerenv ]]; then
  fail "refusing to deploy from inside a container (devcontainer/Codespaces/docker exec).
  'task deploy' starts the production stack on the Docker daemon of the machine it runs on and
  requests real TLS certificates. SSH to the VM and run it there."
fi

# Read KEY's value from a dotenv file: last assignment wins, surrounding
# quotes and trailing CR stripped. Inline comments are not supported (same as
# docker compose's env_file).
value_of() {
  local key=$1 file=$2 v
  v=$(sed -n -E "s/^[[:space:]]*(export[[:space:]]+)?${key}[[:space:]]*=(.*)$/\2/p" "$file" | tail -n1)
  v=${v%$'\r'}
  v=${v#\"}; v=${v%\"}; v=${v#\'}; v=${v%\'}
  printf '%s' "$v"
}

host=$(hostname)
deploy_host=$(value_of DEPLOY_HOST "$env_file")
if [[ -z "$deploy_host" ]]; then
  fail "DEPLOY_HOST is not set in deploy/.env. Set it to the VM's hostname (run 'hostname' on the VM) so
  'task deploy' only runs there."
fi
if [[ "$deploy_host" != "$host" ]]; then
  fail "this machine's hostname ('$host') doesn't match DEPLOY_HOST in deploy/.env ('$deploy_host').
  Run 'task deploy' on the production VM."
fi

if [[ -f "$example" ]]; then
  for key in POSTGRES_PASSWORD KEYCLOAK_ADMIN_PASSWORD; do
    placeholder=$(value_of "$key" "$example")
    if [[ -n "$placeholder" && "$(value_of "$key" "$env_file")" == "$placeholder" ]]; then
      fail "$key in deploy/.env is still the .env.example placeholder. Set a real value."
    fi
  done
  placeholder=$(value_of KEYCLOAK_ADMIN_CLI_SECRET "$example")
  if [[ -n "$placeholder" && "$(value_of KEYCLOAK_ADMIN_CLI_SECRET "$env_file")" == "$placeholder" ]]; then
    echo "deploy preflight: WARNING: KEYCLOAK_ADMIN_CLI_SECRET is still the first-boot placeholder." >&2
    echo "  Fine for a first boot; the API's Keycloak admin calls fail until you set the real" >&2
    echo "  buddy-admin-cli secret (deploy/README.md, step 5)." >&2
  fi
fi

"$dir/release-check.sh" "$(value_of REPOSITORY_URL "$env_file")" || true

echo "deploy preflight: ok (host $host)"
