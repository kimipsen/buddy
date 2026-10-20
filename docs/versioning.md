# Versioning

Buddy's version comes from git tags. Nothing in the repo stores a version number.

## Cutting a release

```bash
task release VERSION=1.2.0   # or: task release -- 1.2.0
```

This tags `HEAD` as an annotated `v1.2.0` tag and pushes `HEAD` plus the tag to every git
remote. It refuses to run if the working tree is dirty, the version isn't a valid SemVer, or
the tag already exists locally or on any remote.

Tags are `v` + [SemVer](https://semver.org): `v1.2.0`, or `v1.3.0-rc.1` for a pre-release.
Deploy from the tagged commit to ship exactly that version.

## How the version is computed

[MinVer](https://github.com/adamralph/minver) rules (tag prefix `v`):

| HEAD                          | Version                  |
| ----------------------------- | ------------------------ |
| tagged `v1.2.0`               | `1.2.0`                  |
| 3 commits after `v1.2.0`      | `1.2.1-alpha.0.3`        |
| 2 commits after `v1.3.0-rc.1` | `1.3.0-rc.1.2`           |
| no `vX.Y.Z` tag yet           | `0.0.0-alpha.0.<height>` |

- **Backend**: the `MinVer` package (`src/backend/buddy/buddy.csproj`) sets the assembly version
  on every `dotnet build`. The SDK appends the commit SHA to the informational version.
- **Docker builds**: the build contexts (`src/backend`, `src/frontend/buddy`) have no `.git`.
  [`deploy/version.sh`](../deploy/version.sh) computes the same version from git on the host.
  `task deploy` and `deploy/azure/deploy.sh` pass it in as the `BUDDY_VERSION` / `BUDDY_COMMIT`
  build args. The API Dockerfile hands them to MinVer (`MinVerVersionOverride`) and the SDK
  (`SourceRevisionId`). The frontend Dockerfile bakes it into the image, and its Caddyfile
  serves it as `version` in `runtime-config.json`. A bare `docker build` without the args
  reports `0.0.0-dev`.

## Where it shows

- `GET /version` (anonymous) returns `{ "version": "1.2.0", "commit": "<sha>" }`.
- The guardian profile menu shows "Version 1.2.0" at the bottom. It is hidden in local
  development, where `public/config/runtime-config.json` has no `version`.

CI checkouts are shallow, so a test build there may compute a different height. Builds that
ship always go through `deploy/version.sh` on a full clone, including the published images below.

## Published images

[`publish-images.yml`](../.github/workflows/publish-images.yml) builds the api, frontend and
keycloak images for `linux/amd64` and `linux/arm64` and publishes them to the GitHub Container
Registry as `ghcr.io/kimipsen/buddy-api`, `buddy-frontend` and `buddy-keycloak`:

| Trigger                          | Image tags                            |
| -------------------------------- | ------------------------------------- |
| tag `v1.5.3`                     | `1.5.3`, `1.5`, `1`, `latest`, `sha-` |
| tag `v1.6.0-rc.1`                | `1.6.0-rc.1`, `sha-`                  |
| push to `master`                 | `edge`, `sha-`                        |
| manual run (`workflow_dispatch`) | `sha-`                                |

`sha-` is `sha-` plus the first 12 characters of the commit, the same as the image tags
`task deploy` gives its local builds. Each image is scanned with Trivy before it is tagged (the
same gate as `security-scan.yml`) and carries build provenance and an SBOM; check one with
`gh attestation verify oci://ghcr.io/kimipsen/buddy-api:1.5.3 --repo kimipsen/buddy`.

The frontend image works for any deployment: it reads `API_BASE_URL`, `KEYCLOAK_AUTHORITY` and
the optional `REPOSITORY_URL` from its environment when it starts.

Both deploy paths can run these images instead of building: `task deploy:pull` on the Oracle VM
([deploy/README.md](../deploy/README.md), step 4) and `BUDDY_IMAGE_VERSION` in
`deploy/azure/.env` ([deploy/README-azure.md](../deploy/README-azure.md), step 3).

## Finding out about new releases

A running installation never checks for updates. The operator watches the repository's
releases on GitHub, and both deploy paths run [`deploy/release-check.sh`](../deploy/release-check.sh),
which warns when the commit being deployed is missing the newest `vX.Y.Z` tag. See
"Staying up to date" in [deploy/README.md](../deploy/README.md) and
[update-check.md](backend/analysis/update-check.md) for why there is no in-app check.
