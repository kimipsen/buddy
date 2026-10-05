# Versioning

Buddy's version comes from git tags. Nothing in the repo stores a version number.

## Cutting a release

```bash
git tag -a v1.2.0 -m "Buddy 1.2.0"
git push origin v1.2.0
```

Tags are `v` + [SemVer](https://semver.org): `v1.2.0`, or `v1.3.0-rc.1` for a pre-release.
Deploy from the tagged commit to ship exactly that version.

## How the version is computed

[MinVer](https://github.com/adamralph/minver) rules (tag prefix `v`):

| HEAD | Version |
| --- | --- |
| tagged `v1.2.0` | `1.2.0` |
| 3 commits after `v1.2.0` | `1.2.1-alpha.0.3` |
| 2 commits after `v1.3.0-rc.1` | `1.3.0-rc.1.2` |
| no `vX.Y.Z` tag yet | `0.0.0-alpha.0.<height>` |

- **Backend**: the `MinVer` package (`src/backend/buddy/buddy.csproj`) sets the assembly version
  on every `dotnet build`. The SDK appends the commit SHA to the informational version.
- **Docker builds**: the build contexts (`src/backend`, `src/frontend/buddy`) have no `.git`.
  [`deploy/version.sh`](../deploy/version.sh) computes the same version from git on the host.
  `task deploy` and `deploy/azure/deploy.sh` pass it in as the `BUDDY_VERSION` / `BUDDY_COMMIT`
  build args. The API Dockerfile hands them to MinVer (`MinVerVersionOverride`) and the SDK
  (`SourceRevisionId`). The frontend Dockerfile writes `version` into `runtime-config.json`.
  A bare `docker build` without the args reports `0.0.0-dev`.

## Where it shows

- `GET /version` (anonymous) returns `{ "version": "1.2.0", "commit": "<sha>" }`.
- The guardian profile menu shows "Version 1.2.0" at the bottom. It is hidden in local
  development, where `public/config/runtime-config.json` has no `version`.

CI checkouts are shallow, so a test build there may compute a different height. Builds that
ship always go through `deploy/version.sh` on a full clone.
