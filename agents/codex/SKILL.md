# Codex Skill — Frontend Development (Angular)

Purpose: Assist frontend development tasks with a focus on Angular (v22).
Project layout requirement: place all frontend source and assets produced by this skill under `src/frontend/` in the repository (e.g., `src/frontend/components`, `src/frontend/services`, `src/frontend/docs`).

Behavioral rules:
- ALWAYS start in "Planning Mode": before producing code, output a concise plan with steps, trade-offs, and required inputs (files, framework versions, constraints).
- Plans MUST include visual artifacts: a wireframe and a proposed screenshot (mockup) of the planned UI. Prefer PNG images; SVG or inline diagrams are acceptable when PNG is not available. For each image provide file paths, alt text, and brief captions.
- Prefer signals and reactive programming patterns (e.g., RxJS, Angular signals) in implementations and recommendations.
- Prefer TailwindCSS for design and layout: use utility classes, keep component-specific CSS minimal, and include guidance for integrating Tailwind into an Angular v22 project.
- When the user doesn't specify, assume Angular v22 and target modern best practices (standalone components, typed signals, reactive forms when applicable).

Usage:
- Start by asking clarifying questions if project context or constraints are missing.
- Provide a short implementation plan, then produce code snippets, tests, and migration notes.

- Generate Angular v22 code following reactive and signal-first patterns.
- Propose step-by-step refactor plans and safe migration paths.
- Output unit/integration test scaffolding and minimal reproducible examples.
C apabilities:
- Generate Angular v22 code following reactive and signal-first patterns.
- Propose step-by-step refactor plans and safe migration paths.
- Prefer TailwindCSS for styling and produce examples using Tailwind utility classes.
- Output unit/integration test scaffolding and minimal reproducible examples.
- Generate Angular v22 code following reactive and signal-first patterns.
- Propose step-by-step refactor plans and safe migration paths.
- Output unit/integration test scaffolding and minimal reproducible examples.

Output format guidance:
- Section 1 — "Plan": bullet list of steps, assumptions, required files, and visual artifacts. Include two images: `wireframe.png` (or `wireframe.svg`) and `mockup.png` (or `mockup.svg`) or equivalent inline diagrams. For each image include a file path, concise alt text, and a one-sentence caption describing intent.
- Section 2 — "Implementation": code blocks with file paths and brief explanations, including Tailwind setup or example utility classes when relevant.
- Section 3 — "Tests": example tests and commands to run them.
- Section 4 — "Notes": trade-offs and compatibility notes (including Tailwind/Angular integration considerations).

Secrets handling:
- **Use `.env` files** to store any secrets or credentials used during development or in examples. Do not commit `.env` to source control; provide a `.env.example` with placeholder values instead. For frontend code, prefer keeping real secrets on the backend and only expose non-sensitive config to the client; if a build-time value is required, document how to inject it during the build (e.g., environment variables or Angular file replacements).
- When producing code that reads secrets, include example code that reads from environment variables (or a `dotenv` loader for local development) and include a short note on secure deployment (CI secret injection, cloud KMS/secret-store, or app settings).

Tags: codex, angular, frontend, signals, reactive, planning
