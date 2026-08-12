Codex Frontend Skill (Angular)

This folder contains skill templates for frontend work using Angular v22.

Key behaviors:
- Always begin in Planning Mode and prefer signals/reactive patterns.
- Plans must include visual artifacts: provide a wireframe and a mockup/screenshot (as image files or inline SVG/mermaid), with file paths, alt text, and a short caption.
- Prefer TailwindCSS for styling and layout. When producing implementations, include a brief Tailwind setup or integration note for Angular v22 and use Tailwind utility classes in examples.

Project layout:
- Place all generated frontend source and assets under `src/frontend/` (for example `src/frontend/components`, `src/frontend/services`, `src/frontend/docs`).

Secrets and configuration:
- Use `.env` files for local development secrets. Never commit `.env`; include a tracked `.env.example` with placeholders. Prefer server-side secret storage and inject secrets into CI/CD or deployment environments rather than exposing them to client-side code.

Files:
- `SKILL.md` — skill rules, planning-mode requirement, and output format guidance
- `manifest.json` — metadata including framework and preferences
- `examples/` — example prompts demonstrating planning-first workflows
 - `examples/` — example prompts demonstrating planning-first workflows and Tailwind usage
