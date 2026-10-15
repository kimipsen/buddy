---
name: codex-pictogram
description: Generate Buddy pictograms (SVG picture symbols for tasks, routine steps, meals, feelings and boards, for children who don't read yet) by delegating the drawing to the Codex CLI (`codex exec`) and its repo `pictogram` skill in .agents/skills/pictogram, which holds the shared style guide so every pictogram looks like part of one set. Collects slug, concept and output directory, runs Codex, then lints and visually checks the result. Use for "make a pictogram for brushing teeth", "generate pictograms for the morning routine", "draw icons for these meals", "ask codex to draw a pictogram", "add a pictogram set". Emoji icons in the existing reward/task inputs are not pictograms; UI components that display pictograms are buddy-frontend.
---

# Pictograms via Codex

Codex draws the pictograms; this skill tells it what to draw and checks what comes back. The style
guide lives in one place, [.agents/skills/pictogram/SKILL.md](../../../.agents/skills/pictogram/SKILL.md),
which Codex loads as its `pictogram` skill. Don't restate or override the style guide in the prompt;
if the style must change, edit that file.

## 1. Pin down the request

For each pictogram you need:

- **slug**: kebab-case English, e.g. `brush-teeth`, `put-on-shoes`, `feeling-tired`.
- **concept**: one concrete sentence, e.g. "a child brushing their teeth at the sink".
- **output directory**: default `src/frontend/buddy/public/pictograms/` (served as `/pictograms/<slug>.svg`).
- **notes** (optional): things to include or avoid.

Derive these from the user's words; ask only if the subject itself is unclear. Check the output
directory for an existing file with the same slug and don't overwrite one unless the user asked.
Send related pictograms (a routine, a feelings set) in **one** Codex run, so they share proportions.

## 2. Run Codex

Run from the repo root of the checkout you're working in (in a worktree, its root), so Codex picks
up `.agents/skills/` and writes there. `$S` is your scratchpad directory.

```bash
codex exec \
  -C "$(git rev-parse --show-toplevel)" \
  -s workspace-write \
  -o "$S/codex-pictogram.md" \
  - <<'EOF'
Use the $pictogram skill (.agents/skills/pictogram/SKILL.md) and follow its workflow and style guide exactly.

Output directory: src/frontend/buddy/public/pictograms/
Write the contact sheet to: <$S>/pictograms.png

Pictograms:
1. slug: brush-teeth
   concept: a child brushing their teeth
   notes: show the toothbrush in the mouth, no sink
2. slug: ...
EOF
```

- Write the real scratchpad path in place of `<$S>` (the heredoc is quoted, so variables don't expand).
- `-s workspace-write` lets Codex write files in the repo and `/tmp`; it doesn't need network.
- Run it with `run_in_background` and a log redirect (`> "$S/codex-pictogram.log" 2>&1`); a set
  of several pictograms takes a few minutes. Wait for the completion notification, don't poll.
- If `codex` isn't logged in (`codex login status`), stop and tell the user to run `codex login`.

## 3. Check the result

1. Read `$S/codex-pictogram.md` (Codex's final message) for the files and interpretations it chose.
2. Lint every file yourself, don't trust the report:
   `node .agents/skills/pictogram/scripts/check-pictogram.mjs src/frontend/buddy/public/pictograms/<slug>.svg ...`
3. Render a contact sheet that includes a few existing pictograms for comparison and look at it:
   `node .agents/skills/pictogram/scripts/render-pictograms.mjs "$S/check.png" <new files> <2-3 existing files>`
   then Read the PNG. Judge: recognisable at 48px, same line weight and fill of the tile as the
   existing ones, readable on the dark background.
4. If something fails, resume the same Codex session with specific fixes so it keeps its context:
   `codex exec resume --last -c sandbox_mode='"workspace-write"' -o "$S/codex-pictogram.md" "Fix brush-teeth.svg: the toothbrush is not visible at 48px; make the handle longer and blue."`
   Two rounds at most; then fix small things (a colour, a coordinate) yourself or report what's left.

## 4. Report

List each file (as a link), what it shows, and show the user the contact sheet path. Mention any
interpretation Codex chose. Don't commit; landing is `rebase-commit`.
