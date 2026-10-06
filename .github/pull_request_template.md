## What and why

<!-- What does this change, and why? Link the issue or analysis doc (docs/*/analysis/) if there is one. -->

## How it was tested

<!-- Commands run, manual checks, screenshots for UI changes. -->

## Checklist

- [ ] Tests added or updated, and `task test` passes (or the relevant `task test:*`).
- [ ] Persisted events: no renamed or removed fields; new events have golden files.
- [ ] UI strings in both English and Danish (`node .claude/skills/i18n/check-parity.mjs`).
- [ ] A new page or a visible page change updates `screenshots/pages.ts` and `task docs:screenshots`.
- [ ] Docs updated (README, `docs/`, the analysis doc's status).
- [ ] No secrets, and no personal data in logs (see `docs/backend/observability.md`).
