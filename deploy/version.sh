#!/usr/bin/env bash
# Prints the build version derived from git tags, using the same rules as MinVer in
# src/backend/buddy/buddy.csproj (tag prefix "v"), so a Docker build (whose context has no .git)
# gets the version a local `dotnet build` would compute:
#
#   HEAD tagged v1.2.0                        -> 1.2.0
#   3 commits after v1.2.0                    -> 1.2.1-alpha.0.3
#   2 commits after v1.3.0-rc.1               -> 1.3.0-rc.1.2
#   no vX.Y.Z tag yet, 338 commits after root -> 0.0.0-alpha.0.338
#
# Usage: deploy/version.sh            -> version
#        deploy/version.sh --commit   -> full commit SHA
set -euo pipefail

cd "$(dirname "$0")/.."

if [[ "${1:-}" == "--commit" ]]; then
  git rev-parse HEAD
  exit 0
fi

tag=$(git describe --tags --abbrev=0 --match 'v[0-9]*.[0-9]*.[0-9]*' 2>/dev/null || true)

if [[ -z "$tag" ]]; then
  # MinVer counts the root commit as height 0.
  echo "0.0.0-alpha.0.$(($(git rev-list --count --first-parent HEAD) - 1))"
  exit 0
fi

version=${tag#v}
height=$(git rev-list --count --first-parent "$tag..HEAD")

if [[ "$height" == 0 ]]; then
  echo "$version"
elif [[ "$version" == *-* ]]; then
  echo "$version.$height"
else
  IFS=. read -r major minor patch <<<"${version%%+*}"
  echo "$major.$minor.$((patch + 1))-alpha.0.$height"
fi
