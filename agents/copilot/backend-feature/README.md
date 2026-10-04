Backend Feature Skill

Recipe for shipping a Buddy backend use case end to end in `src/backend/buddy`: command/query, validator, Wolverine handler, minimal-API endpoint, feature wiring, event and snapshot updates, integration tests, and related docs. Use it when adding a new backend feature, aggregate, or domain event.

It lives at `agents/copilot/backend-feature/` as a Copilot/Codex/GitHub Pilot agents package skill.

Files:
- `SKILL.md` - The full backend-feature workflow and checklists, ported without YAML frontmatter.
- `manifest.json` - Metadata for the Copilot agents package version of the skill.
- `README.md` - Short description of the skill and the files shipped in this directory.
- `templates/` - Copied compile-verified skeleton files for new backend slices and tests.
