#!/usr/bin/env bash
# The image digests a release publishes (publish-images.yml, release-assets job; design:
# docs/backend/analysis/publish-docker-images.md#decision-publish-the-image-digests-with-each-release).
#
#   release-digests.sh assets <in-dir> <out-dir>
#     Joins the per-image files the publish job wrote (buddy-<image>.json: name, repository, tag,
#     index digest, per-platform digests) into <out-dir>/image-digests.json and
#     <out-dir>/image-digests.txt (one <repository>:<tag>@<digest> per line). Fails unless there is
#     exactly one file each for api, frontend and keycloak, and unless each digest still matches
#     `docker buildx imagetools inspect <repository>:<tag>`. Reads VERSION and COMMIT.
#
#   release-digests.sh notes <image-digests.json> <body-file>
#     Prints <body-file> (the release notes) with the "Container images" section added at the end,
#     or replaced where an earlier run put it. Everything outside the section's markers, notes
#     written by hand included, stays as it is. Reads GH_REPO for the verify command.
set -euo pipefail

IMAGES=(buddy-api buddy-frontend buddy-keycloak)
START='<!-- image-digests:start -->'
END='<!-- image-digests:end -->'

fail() { echo "release-digests: $*" >&2; exit 1; }

assets() {
  local in=$1 out=$2 name file expected actual
  : "${VERSION:?}" "${COMMIT:?}"
  mkdir -p "$out"

  for name in "${IMAGES[@]}"; do
    [[ -f "$in/$name.json" ]] || fail "no digest file for $name in $in"
  done
  [[ $(find "$in" -maxdepth 1 -name '*.json' | wc -l) -eq ${#IMAGES[@]} ]] \
    || fail "expected exactly ${#IMAGES[@]} digest files in $in: $(ls "$in")"

  # The tag the publish job read the digest from may move (a later re-run); the release lists what
  # the tag points at now, and fails rather than publish a digest that doesn't match it.
  for name in "${IMAGES[@]}"; do
    file="$in/$name.json"
    jq -e --arg version "$VERSION" '.tag == $version' "$file" > /dev/null \
      || fail "$file is for tag $(jq -r .tag "$file"), not $VERSION"
    expected=$(jq -er .digest "$file")
    actual=$(docker buildx imagetools inspect "$(jq -er '.repository + ":" + .tag' "$file")" \
      --format '{{json .Manifest}}' | jq -er .digest)
    [[ "$expected" == "$actual" ]] || fail "$name:$VERSION is $actual in the registry, not $expected"
  done

  local files=()
  for name in "${IMAGES[@]}"; do files+=("$in/$name.json"); done
  jq -s --arg version "$VERSION" --arg commit "$COMMIT" \
    '{version: $version, commit: $commit,
      images: (sort_by(.name) | map(. + {reference: "\(.repository):\(.tag)@\(.digest)"}))}' \
    "${files[@]}" > "$out/image-digests.json"
  jq -r '.images[].reference' "$out/image-digests.json" > "$out/image-digests.txt"
}

section() {
  local json=$1 repo=${GH_REPO:?}
  jq -r --arg start "$START" --arg end "$END" --arg repo "$repo" '
    (.images[0]) as $first
    | [
        $start,
        "## Container images",
        "",
        "The images this release built, by digest. Pin a reference to deploy exactly these images; a tag like `\(.version)` or `latest` can move. `image-digests.json` adds each architecture'"'"'s digest.",
        "",
        "| Image | Reference |",
        "| --- | --- |",
        (.images[] | "| `\(.name)` | `\(.reference)` |"),
        "",
        "Check an image and its build provenance:",
        "",
        "```sh",
        "docker buildx imagetools inspect \($first.repository):\(.version)",
        "gh attestation verify oci://\($first.repository)@\($first.digest) --repo \($repo)",
        "```",
        $end
      ]
    | .[]' "$json"
}

notes() {
  local json=$1 body=$2 kept
  # Drops an earlier section. Bodies edited on github.com come back with CRLF line endings.
  kept=$(awk -v start="$START" -v end="$END" '
    { sub(/\r$/, "") }
    $0 == start { skip = 1; next }
    $0 == end { skip = 0; next }
    !skip' "$body")
  # $(...) has dropped the trailing blank lines.
  if [[ -n "${kept//[[:space:]]/}" ]]; then
    printf '%s\n\n' "$kept"
  fi
  section "$json"
}

case "${1:-}" in
  assets) [[ $# -eq 3 ]] || fail "usage: $0 assets <in-dir> <out-dir>"; assets "$2" "$3" ;;
  notes) [[ $# -eq 3 ]] || fail "usage: $0 notes <image-digests.json> <body-file>"; notes "$2" "$3" ;;
  *) fail "usage: $0 assets <in-dir> <out-dir> | notes <image-digests.json> <body-file>" ;;
esac
