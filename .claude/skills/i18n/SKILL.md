---
name: i18n
description: Add, rename, reword or remove user-facing UI strings in the Buddy Angular frontend's typed English/Danish translation dictionaries (src/frontend/buddy/src/app/core/i18n), wire them up through the translate pipe or TranslationService, and verify en/da parity with the bundled check-parity.mjs script. Use for "add a translation", "add the Danish text", "this string is hardcoded", "translate this label", "add i18n keys for the new component", "check for missing translations", "find unused translation keys", or any frontend change that adds or changes visible text.
---

# i18n — Buddy UI strings (en + da)

All paths below are relative to `src/frontend/buddy/src/app/core/i18n/` unless they say otherwise.

## How it's built (verified)

- `translations/{en,da}/<area>.ts` — one file per feature area, each a plain object literal: `export const dashboard = { doses: { title: 'Today’s medicine', ... } }`. Areas: admin, calendar, child, common, dashboard, events, invite, login, mealplan, medicine, pickup, profile, progress, shell, taskLibrary (`task-library.ts`), verifyEmail (`verify-email.ts`).
- `translations/{en,da}/index.ts` — imports every area and merges them as `export const en = { admin, calendar, ... }`. A new area file must be added to **both** index files.
- `translations/index.ts` — `TRANSLATIONS: Record<Language, typeof en> = { en, da }` plus a type-level exact check (`TranslationKeyParity`) that compares every dotted key path of `en` and `da`. A key **missing in da** *or* **extra in da**, at any depth, fails `npx tsc --noEmit -p tsconfig.app.json` / `ng build` with `TS2344: Type '{ missingInDa: ...; extraInDa: "common.foo"; }' does not satisfy the constraint 'true'` — the message names the offending keys. (Plain `Record<…, typeof en>` alone would miss extra keys, since `da` is a variable, not a fresh literal.) The parity script still runs in CI (`.github/workflows/frontend-tests.yml`) for `{placeholder}` mismatches and identical-text warnings, which the type check can't see.
- A key is the dotted path: `dashboard.doses.title`. `TranslationService.translate(key, params)` walks the path; an unknown key or a key that points at an object renders **the raw key** at runtime — no error.
- Placeholders are single braces, not `{{ }}`: `starCount: '{count} stars'` → `{{ 'dashboard.children.starCount' | translate: { count: starCount } }}`; in TS, `translation.translate('calendar.agenda.dayTitle', { date: ... })`.
- `language.ts` — `SUPPORTED_LANGUAGES = ['en', 'da']`, `DEFAULT_LANGUAGE = 'en'`; browser language seeds it pre-auth, then the user's saved language from `GET /users/me` replaces it.
- `translate.pipe.ts` — `TranslatePipe` (`translate`, impure so it re-renders on language change). Import it in the component's `imports: [...]`.

## Usage patterns in this codebase

- Template literal key: `{{ 'dashboard.doses.title' | translate }}`, also in bindings: `[label]="'dashboard.doses.loading' | translate"`.
- Dynamic status/error messages: store the **key** on the signal and translate in the template — `this.error.set('dashboard.doses.loadError')` + `@if (error(); as message) { {{ message | translate }} }` (see `features/guardian/doses-today/`). A raw backend validation message is passed through untranslated.
- Enum → key maps: `Record<number, string>` of full keys (`features/guardian/admin/manage-groups/manage-groups.ts`, `features/child/mealplan/child-mealplan.ts`). Prefer this over building keys by concatenation, so every key appears literally in source.
- In TS (option labels for `app-segmented-control` etc.): `inject(TranslationService).translate('pickup.cell.kind.guardian')` (`features/guardian/pickup/pickup-cell/pickup-cell.ts`). Wrap it in a `computed()` (as `kindOptions` there does) so the labels follow a language switch.
- Dynamic concatenation exists in two places only: `'shell.menu.theme.' + mode` (`features/guardian/shell/profile-menu/profile-menu.html`) and `'child.home.theme.' + mode` (`features/child/home/child-menu/child-menu.html`).

## Steps

1. **Find the right area and nesting.** Put the key in the area file matching the feature (`features/guardian/medicine/*` → `medicine.ts`, dashboard widgets → `dashboard.ts`, a component under `admin/manage-x` → `admin.manageX.*`). Follow the existing sub-structure (`title`, `loading`, `loadError`, `empty`, `form.*Label`, `...Button`); reuse `common.*` (e.g. `common.loading`, `common.selectChildLabel`, `common.colorLabel`) instead of duplicating.
2. **Add the key in en and da in the same edit**, same position in both files, same nesting. Never leave a TODO in one language.
3. **Danish must be real Danish**, not the English copied over. Match the tone of nearby da strings (e.g. `Indlæser …`, `Kunne ikke indlæse …`, `Tilføj`, `Gem`, `Annuller`, informal "du"). If you are not sure of the wording, still write your best Danish and **flag the key in your final message** for review by a Danish speaker. Identical en/da values are only fine for names, brands and loanwords that Danish actually uses (`Buddy`, `System`, `Stop`, `Live`, `Send`).
4. **Keep placeholders identical** in both languages (`{count}`, `{date}`); the parity check fails on mismatch.
5. **Use it** via the pipe in templates (add `TranslatePipe` to the component's `imports`) or `TranslationService.translate` in TS. No hardcoded user-facing text in templates or `aria-label`s, including `sr-only` labels.
6. **Renaming or removing a key**: grep for the old key across `src/frontend/buddy/src` and `src/frontend/buddy/e2e` first, and update all usages in the same change.
7. **Run the parity check** (from any directory):
   ```bash
   node .claude/skills/i18n/check-parity.mjs            # from repo root
   node ../../../.claude/skills/i18n/check-parity.mjs   # from src/frontend/buddy
   ```
   Exit 1 = keys missing in en or da, or `{placeholder}` mismatch — fix before finishing. Identical-value entries are warnings: confirm each is intentional. Add `--unused` to list keys no source file references (warnings only; keys reachable only via a `'prefix.' + x` concatenation are listed separately as "possibly unused" — check those by hand). `--json` for machine output, `--no-identical` to hide the identical-value list.
8. **Run the type check and affected specs** (from `src/frontend/buddy`):
   ```bash
   npx tsc --noEmit -p tsconfig.app.json
   npx ng test --watch=false --include src/app/path/to/component.spec.ts
   ```
   Specs render the **English** strings (jsdom's browser language is `en-US`, and `TranslatePipe`/`TranslationService` are used unstubbed), and assert on them — e.g. `textContent` contains `'Danger zone'`. Changing English copy can break unit specs **and** Playwright e2e specs (`e2e/*.spec.ts` select by visible English text, e.g. `getByRole('button', { name: 'Add schedule' })`). Grep for the old English text in `src/**/*.spec.ts` and `e2e/` and update them.

## Gotchas

- An unknown key never throws — a typo just renders `dashboard.dose.title` on screen. The type check doesn't validate key strings in templates; the parity check `--unused` and a spec asserting the English text are what catch it.
- Don't build keys by string concatenation for new code; a literal map of full keys keeps `--unused` accurate.
- Keys used only in a spec show up as unused — spec files are deliberately not counted as usage. `--unused` currently reports none (the last 7 dead keys, e.g. `common.signOut`, `common.genericError`, `child.home.guardiansTitle`, were removed; `translation.service.spec.ts` now uses `common.colorLabel`). Keep it at zero: remove a key in the same change that removes its last usage.
