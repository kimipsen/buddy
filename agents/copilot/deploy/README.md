Copilot skill package for Buddy production deployments. Use it when checking readiness for an Oracle VM or Azure Container Apps deploy, asking for explicit approval, running the deployment with a log, verifying the live services, and guiding rollback or backup follow-ups.

Files:
- `SKILL.md` — deployment decision tree, prerequisite checks, approval rules, run steps, verification commands, rollback guidance, and reporting checklist.
- `manifest.json` — skill metadata.
- `check-env.sh` — env-file checker that reports missing, empty, placeholder, and extra keys without printing secret values.
