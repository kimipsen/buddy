# Load tests (k6)

Performance and load tests for the Buddy API, written for [k6](https://grafana.com/docs/k6/latest/)
in TypeScript. The design, the workload model and the thresholds are in
[load-testing-k6.md](../../docs/backend/analysis/load-testing-k6.md).

## Run

```bash
task load:stack:up                    # isolated stack: Postgres, Keycloak, the API built from the working tree, Grafana
task test:load                        # smoke (2 families, every journey twice)
task test:load PROFILE=load VUS=50    # 50 families for 10 minutes (HOLD=10m)
task test:load PROFILE=rate-limit     # the limiter answers 429, never 5xx, and recovers
task load:stack:down                  # stop it and delete its data
```

- Profiles: `smoke`, `load`, `stress`, `spike`, `soak`, `breakpoint`, `rate-limit` (one file each
  in `scenarios/`).
- Knobs (all optional): `FAMILIES`, `VUS`, `HOLD`, `STEPS` (stress), `RATE_STEP`/`MINUTES`/`MAX_VUS`
  (breakpoint), `SEED_HISTORY_DAYS`, `THINK_TIME=0|1`, `CLEANUP=0`.
- `TARGET=dev` runs `smoke` against the devcontainer's running API (`http://localhost:5193`) and
  Keycloak, and removes its `load.*` users afterwards. Nothing heavier runs on the shared dev stack.
- `BUDDY_API_IMAGE=<image> task load:stack:up` runs a published API image instead of building one.

Each run writes `results/<profile>-<timestamp>/summary.json` and `report.html` (git-ignored).
While a run is going, Grafana on <http://localhost:3300> shows k6's metrics next to the API's
traces and metrics (ASP.NET Core, Npgsql, Wolverine).

## Layout

| Folder       | What's in it                                                                          |
| ------------ | ------------------------------------------------------------------------------------- |
| `lib/`       | config and thresholds, Keycloak tokens, the typed API client, seeding, setup/teardown |
| `journeys/`  | what one user does: `guardian-morning`, `guardian-writes`, `child-board`, `ical-poll` |
| `scenarios/` | one file per profile: which journeys, how many VUs, which stages                      |
| `scripts/`   | `summarize.mjs`, the p95 table the nightly workflow writes to its job summary         |

Routes and request bodies are typed against the frontend's generated OpenAPI types
(`src/frontend/buddy/src/app/core/api/buddy-api.ts`). After an API change, `task docs:openapi`
then `task test:load:typecheck` shows which journeys need updating.

When a feature adds a screen guardians or children visit often, add its requests to the matching
journey in the same change.
