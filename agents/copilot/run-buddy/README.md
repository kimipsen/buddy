Run Buddy Skill

Operational guidance for launching the real Buddy stack in the devcontainer, verifying health, logging in as seeded users, taking screenshots with Playwright, inspecting Marten/Postgres data, reading Mailpit mail, and shutting everything down safely. Use it when you need to run, demo, or verify the app locally.

It lives at `agents/copilot/run-buddy/` as a Copilot/Codex/GitHub Pilot agents package skill.

Files:
- `SKILL.md` - The full run/start/inspect/stop workflow, with copied-path references updated for this agents package.
- `manifest.json` - Metadata for the Copilot agents package version of the skill.
- `README.md` - Short description of the skill and the files shipped in this directory.
- `screenshot.mjs` - Copied Playwright helper for logging in and capturing screenshots of Buddy pages.
