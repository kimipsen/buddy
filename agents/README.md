# Agent packages

This directory contains reusable agent skills, examples, templates, and
reference implementations. Choose the package that matches the task rather
than combining overlapping instructions by default.

## Packages

- [Codex frontend skill](codex/README.md) — plans and implements Angular 22
  frontend work using signals, reactive patterns, Tailwind CSS, tests, and
  visual planning artifacts.
- [Copilot documentation skill](copilot/README.md) — creates and updates
  architecture records, API/service documentation, component documentation,
  changelog entries, and related templates.
- [GitHub Pilot skill](github-pilot/README.md) — supports patch suggestions,
  pair-programming prompts, PR descriptions, review checklists, tests, and
  migration guidance.

Each package keeps its behavior in `SKILL.md`, machine-readable metadata in
`manifest.json`, and task examples under `examples/`. Some packages also
include `templates/` or executable `samples/`; their README identifies which
artifacts are intended to be copied, adapted, or used only as reference.

## Maintenance

- Keep README claims, skill capabilities, examples, and manifest descriptions
  aligned.
- Treat `{{Placeholder}}` in a template as a value the generating agent must
  replace, not literal output.
- Keep examples free of credentials. Use `.env.example` placeholders when a
  workflow needs environment configuration.
- Validate executable samples against the framework version named by the
  package before using them as recommended patterns.
