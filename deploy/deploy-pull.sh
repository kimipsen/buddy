#!/usr/bin/env bash
# Second half of `task deploy:pull` (after preflight.sh): starts the production stack from the
# images publish-images.yml published, instead of building them on the VM like `task deploy`.
# See deploy/README.md "Deploying a published release".
#
# The images come from the registry, but docker-compose.prod.yml, the edge Caddyfile and .env
# come from this checkout, so the two must belong to the same release. With no version, it
# deploys the release tag HEAD is on and refuses when HEAD isn't on one; with a version (1.5.3,
# or edge / sha-<commit> to try out master) it warns when HEAD isn't on v<version>.
#
# BUDDY_IMAGE_REGISTRY in .env picks the registry (a fork's ghcr.io/<owner>); blank means
# ghcr.io/kimipsen.
#
# Usage: deploy/deploy-pull.sh [version]
set -euo pipefail

dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$dir"

fail() { echo "deploy pull: $*" >&2; exit 1; }
warn() { echo "deploy pull: WARNING: $*" >&2; }

# Same dotenv reading as preflight.sh: last assignment wins, quotes and CR stripped.
value_of() {
  local key=$1 file=$2 v
  v=$(sed -n -E "s/^[[:space:]]*(export[[:space:]]+)?${key}[[:space:]]*=(.*)$/\2/p" "$file" | tail -n1)
  v=${v%$'\r'}
  v=${v#\"}; v=${v%\"}; v=${v#\'}; v=${v%\'}
  printf '%s' "$v"
}

version=${1:-}
version=${version#v}
head_tag=$(git describe --tags --exact-match --match 'v[0-9]*.[0-9]*.[0-9]*' HEAD 2>/dev/null || true)

if [[ -z "$version" ]]; then
  [[ -n "$head_tag" ]] || fail "HEAD isn't on a release tag, so there's no version to deploy.
  Check out the release first (git fetch --tags && git checkout v1.5.3), or pass one: task deploy:pull VERSION=1.5.3"
  version=${head_tag#v}
elif [[ "$head_tag" != "v$version" ]]; then
  warn "HEAD isn't on v$version. docker-compose.prod.yml, the Caddyfile and .env come from this
  checkout and may not match the $version images. To deploy a release, check out its tag first."
fi

registry=$(value_of BUDDY_IMAGE_REGISTRY .env)
registry=${registry:-ghcr.io/kimipsen}

# Read by docker-compose.prod.yml's image names. Not in .env on purpose: `task deploy` must keep
# naming its local builds buddy-<service>:<sha>.
export BUDDY_IMAGE_PREFIX="${registry%/}/" BUDDY_IMAGE_TAG="$version"

echo "Deploying ${BUDDY_IMAGE_PREFIX}buddy-{api,frontend,keycloak}:$version"

# docker pull rather than `compose pull`, which may only warn when a service that has a build:
# section can't be pulled. Each pull picks this machine's architecture from the multi-arch image.
for svc in api frontend keycloak; do
  docker pull "${BUDDY_IMAGE_PREFIX}buddy-$svc:$version"
done

# --no-build: never build the checkout under the published name.
docker compose -f docker-compose.prod.yml up -d --no-build --wait --wait-timeout 600

# Only reached when every service came up: buddy-<service>:latest = last good deploy, as after
# `task deploy`.
for svc in api frontend keycloak; do
  docker tag "${BUDDY_IMAGE_PREFIX}buddy-$svc:$version" "buddy-$svc:latest"
done
