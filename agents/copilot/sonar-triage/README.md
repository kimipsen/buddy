Copilot skill package for triaging SonarCloud and SonarQube findings in Buddy. Use it when the user pastes findings or wants issues fetched from the Sonar API, classified against the repo's known false positives, fixed only when they are real, and reported in a table with the right follow-up in Sonar.

Files:
- `SKILL.md` — triage workflow, verdict criteria, fix rules, test expectations, and reporting format.
- `manifest.json` — skill metadata.
- `fetch-issues.mjs` — helper script that fetches or parses Sonar issue lists and summarizes them by rule.
