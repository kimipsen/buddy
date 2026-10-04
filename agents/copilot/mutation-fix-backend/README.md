Backend Mutation Fix Skill

Run Stryker.NET on a small backend scope, triage surviving and uncovered mutants, strengthen Alba/xUnit integration tests, and rerun on that same scope until every relevant survivor is gone or clearly explained. Use it for hardening one feature slice, a changed set of backend files, or a named backend mutation-testing target.

Files:
- `SKILL.md` — backend mutation-fix loop, scoped-config workflow, triage rules, rerun rules, and reporting guidance.
- `manifest.json` — skill metadata.
- `README.md` — package overview and file list.
- `scoped-config.mjs` — helper that builds a scoped Stryker.NET config from the checked-in config plus an explicit file/glob list.
- `survivors.mjs` — report helper that lists `Survived` and `NoCoverage` backend mutants for a chosen file set.
