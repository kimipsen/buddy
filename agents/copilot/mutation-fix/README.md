Mutation Fix Skill

Run the next frontend mutation-testing batch, triage surviving and uncovered mutants, strengthen Angular/Vitest specs, and rerun Stryker on the same files until every relevant survivor is gone or clearly explained. Use it for `task test:mutation:frontend:batch` follow-up work, explicit survivor cleanup, or a manual file-scoped mutation pass.

Files:
- `SKILL.md` — frontend mutation-fix loop, triage rules, rerun rules, reporting, and notification guidance.
- `manifest.json` — skill metadata.
- `README.md` — package overview and file list.
- `progress.sh` — helper script for 5-minute Stryker progress updates from a batch or rerun log.
- `survivors.mjs` — report helper that lists `Survived` and `NoCoverage` mutants for a chosen file set.
