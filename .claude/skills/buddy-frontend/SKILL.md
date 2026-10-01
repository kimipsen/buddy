---
name: buddy-frontend
description: Buddy Angular 22 frontend conventions (src/frontend/buddy) - zoneless standalone components with signals, Tailwind 4 with light/dark/system theming, the core/*.service.ts API layer over runtime config, postIdempotent for POSTs, Keycloak PKCE auth with guards and interceptor, role-split guardian/child routes, shared controls in src/app/shared, accessible button-role controls, and Vitest specs (settle() flush, stubbed services, HttpTestingController). Use when creating or changing an Angular component, page, route, frontend service method, template, styling/dark mode, guard, or frontend unit spec, e.g. "add a screen for X", "add a widget to the guardian dashboard", "call the new endpoint from the frontend", "fix this component", "write the spec for this component".
---

# Buddy frontend (Angular 22)

App root: `src/frontend/buddy`. All paths below are relative to it unless they start with `docs/` or `.claude/`. Background: `docs/frontend/README.md` (routes, services, i18n, theming) and `README.md` (layout, commands).

## Working rules

- **Plan first** for anything beyond a one-file fix: list the files to add/change (component, spec, route, service method, i18n keys, e2e), assumptions and trade-offs, then implement. No mockups/wireframes needed.
- **Signals first.** State is `signal()`/`computed()`/`effect()`; inputs/outputs are `input()`/`output()`; DI is `inject()` in field initializers (no constructor injection, no `@Input`/`@Output`, no `toSignal`, no `subscribe()` in components — services return `Promise`s and components `await` them).
- Match the existing feature-first layout; don't accept CLI scaffold paths blindly. Keep changes minimal; don't add signals or branches nothing reads (recent mutation-hardening commits deleted exactly that kind of dead state).
- Never hardcode endpoints or user-facing text: endpoints come from runtime config, text from the i18n dictionaries (use the `i18n` skill).

## Verified conventions

**Bootstrap / zoneless.** No `zone.js` in `package.json`; `src/app/app.config.ts` provides only `provideBrowserGlobalErrorListeners()`, `provideAppInitializer(() => inject(RuntimeConfigService).load())`, `provideHttpClient(withInterceptors([authInterceptor]))`, `provideRouter(routes)`. Change detection is driven by signals; don't reach for `NgZone`, `ChangeDetectorRef.detectChanges()` or `OnPush` (no component sets it — zoneless makes it moot).

**Components.** Standalone by default (no `standalone:` flag, no NgModules), `templateUrl` + no component stylesheet, files `name.ts` / `name.html` / `name.spec.ts` in their own folder, class names without a `Component` suffix (`DosesToday`, `GuardianShell`; `AiProviderSettingsComponent` is the lone exception). Built-in control flow only (`@if`/`@for ... track`/`@else if`; `@if (error(); as message)`). Template-driven forms with `FormsModule`/`ngModel` (no reactive forms). Example: `src/app/features/guardian/doses-today/` — `loading`/`error`/`savingKey` signals, `ngOnInit(): void { void this.load(); }`, `try/catch/finally` that sets an i18n **key** on the error signal. Note `tsconfig.json` has `strictTemplates: false`, so template type errors are not caught by the build — be careful with template expressions.

**Styling (Tailwind 4).** `src/styles.css` does `@import 'tailwindcss'` (PostCSS via `.postcssrc.json`, no `tailwind.config`). Utilities inline in templates; palette is `slate` neutrals + `emerald` accent + `red` errors, `rounded-lg` cards with `border-slate-200 bg-white shadow-sm`. **Every colour utility needs a `dark:` pair** (`bg-white dark:bg-slate-900`, `text-slate-500 dark:text-slate-400`, `border-slate-200 dark:border-slate-800`), including `[class.x]` bindings (`[class.dark:bg-slate-600]="!checked()"` in `src/app/shared/toggle/toggle.html`). Dark mode is class-based: `@custom-variant dark (&:where(.dark, .dark *));`.

**Theming.** `core/theme.ts` (`ThemeMode = 'light' | 'dark' | 'system'`, default `system`), `core/theme-storage.ts` (localStorage key `buddy_theme_mode`), `core/theme.service.ts` (`mode`, `isDark` computed, live `matchMedia('(prefers-color-scheme: dark)')` listener, `setMode`). `src/app/app.ts` toggles `.dark` on `<html>` in an `effect`; `src/index.html` applies it pre-boot to avoid a flash. Theme switchers: `features/guardian/shell/profile-menu/` and `features/child/home/child-menu/`. (`themes/buddy/` at the repo root is the **Keycloak** login/email theme, built separately with the Tailwind CLI in `themes/buddy/build/` — not the Angular app's theme.)

**API layer.** One `@Injectable({ providedIn: 'root' })` service per backend area in `src/app/core/*.service.ts` (e.g. `medicines.service.ts`), with request/response interfaces exported next to it. Pattern: `private readonly http = inject(HttpClient)`, `private readonly runtimeConfig = inject(RuntimeConfigService)`, methods return `Promise<T>` via `firstValueFrom(this.http.get<T>(\`${this.runtimeConfig.apiBaseUrl}/...\`))`. Backend enums travel as numeric ordinals (`type DoseStatus = 0 | 1 | 2` with a comment mapping them). Runtime config: `public/config/runtime-config.json` (`keycloak.{authority,realm,clientId,redirectPath}`, `apiBaseUrl`) loaded by `core/runtime-config.service.ts` before the app starts — public values only, never secrets.

**POSTs.** Every create-style POST goes through `postIdempotent<T>(this.http, url, body)` from `core/http-idempotency.ts` (fresh `Idempotency-Key` per call, retries status 0/5xx with the same key, rethrows 4xx). PUT/PATCH/DELETE use plain `http.put/patch/delete` (already idempotent server-side).

**Auth.** `core/auth.service.ts` — Keycloak authorization-code + PKCE (`core/pkce.ts`), tokens in sessionStorage (`core/token-storage.ts`), refresh with skew, `isAuthenticated` computed. `core/auth.interceptor.ts` adds `Authorization: Bearer` only to requests under `apiBaseUrl`. `core/auth.guard.ts` completes the redirect, sends unauthenticated users to `/login`, best-effort `users.ensureCurrentUser()`. `core/role.guard.ts` (`roleRedirectGuard` on `''`) resumes pending invite/verify tokens (`core/pending-*-token.ts`) and redirects to `/child` or `/guardian` from `AccountService.resolveRole()` (role derived from guardian links, not a flag).

**Routing by role.** `src/app/app.routes.ts`: public `login`, `invite/:token`, `guardian-invite/:token`, `verify-email/:token`; `guardian` and `child` lazy-load `features/guardian/guardian.routes.ts` (`GUARDIAN_ROUTES`, children of `GuardianShell`) and `features/child/child.routes.ts` (`CHILD_ROUTES`, no shell) behind `authGuard`. New guardian page → add a child route there; dashboard widgets are components placed in `features/guardian/dashboard.html`.

**Shared components** (`src/app/shared/`): `toggle` (boolean switch), `segmented-control` (radio-group of buttons), `stepper`, `date-select`, `time-select`, `color-swatch-picker`, `repeatable-row`, `progress-badge`, `loading-spinner` (`<app-loading-spinner [label]="'x.loading' | translate" />`). Reuse them before writing a new control.

**Accessibility.** Booleans use `app-toggle` — `<button type="button" role="switch" [attr.aria-checked]>` — never a native checkbox (commits b1eb72a, e89bd42). Choice-of-N uses `app-segmented-control` (`role="radiogroup"` + `role="radio"` `aria-checked` buttons). All buttons `type="button"`; icon-only buttons get an `sr-only` span or `aria-label` (translated); inputs get a `<label for>` (`sr-only` if visually hidden); decorative spinners are `aria-hidden="true"`. Child-facing touch targets use `size="lg"` (~44px).

**Specs.** Colocated `*.spec.ts`, Vitest API via Angular's unit-test builder. Component specs stub services with `{ provide: XService, useValue: Partial<XService> }` + `vi.fn`, flush async with `settle()` (macrotask), not `whenStable()`; service specs use `provideHttpClient()` + `provideHttpClientTesting()` + `HttpTestingController` with a stubbed `RuntimeConfigService`. Query toggles by `button[role="switch"]`/`aria-checked`. `src/test-setup.ts` stubs `matchMedia`. Details and copyable snippets: `spec-patterns.md`.

## New component / feature checklist

1. **Plan** the file list (see Working rules).
2. **Service method** in the matching `src/app/core/*.service.ts` (+ exported types); `postIdempotent` for POST. Add/extend its spec in `core/*.service.spec.ts` with exact URL/method/body assertions.
3. **Component** in `src/app/features/<guardian|child>/<area>/<name>/` (or `src/app/shared/` if reusable), signals + `inject()`, `TranslatePipe` in `imports`, light **and** dark classes, shared controls, a11y as above.
4. **Spec** next to it: loading, empty, error, success, and each user action (assert the exact service call args and the rendered English text).
5. **Route**: add to `guardian.routes.ts` / `child.routes.ts` (or place it on the dashboard/parent page), and a navigation link if needed.
6. **i18n keys in both en and da** — follow the `i18n` skill (`.claude/skills/i18n/SKILL.md`) and run its parity check.
7. **E2E**: for a new user-visible workflow, add or extend a Playwright spec in `e2e/` (use `loginAs(SEEDED_USERS.x)` from `e2e/support/auth-fixture.ts` and helpers in `e2e/support/guardian-data.ts`; see `e2e/medicine-dose-status.spec.ts`). If you change English copy, update e2e selectors that match it.
8. **Docs**: tick/add the item in the root `README.md` `## Features` list, and update `docs/frontend/README.md` (routes, responsibilities, shared services) when routes or services change.
9. Run the commands below.

## Commands (from `src/frontend/buddy`)

```bash
npx tsc --noEmit -p tsconfig.app.json                           # type check app (~5s)
npx tsc --noEmit -p tsconfig.spec.json                          # type check specs
npx ng test --watch=false --include src/app/path/to/foo.spec.ts # one spec
npm test -- --watch=false                                       # whole unit suite (task test:frontend from repo root)
npm run build                                                   # production build (budget: 500kB warn / 1MB error initial)
npm run test:e2e                                                # Playwright; needs Postgres/Keycloak/Mailpit (devcontainer)
node ../../../.claude/skills/i18n/check-parity.mjs              # en/da parity
npm run lint                                                    # ESLint via ng lint (src/**/*.ts, src/**/*.html, e2e/**/*.ts); warnings allowed
npm run format:check                                            # prettier --check . (CI); `npm run format` to write
```

ESLint is `angular-eslint` with a flat config in `eslint.config.js` (typescript-eslint recommended + stylistic, angular ts/template recommended + template accessibility, `eslint-config-prettier` last). Lint must exit 0; the only warn-level rule is `@angular-eslint/template/interactive-supports-focus` (existing click-to-dismiss backdrops). Prettier (`.prettierrc`: width 100, single quotes, angular parser for HTML; `.prettierignore` for build/report output and `public/config`) is enforced in CI, so run `npx prettier --write <files>` on files you change. The one-off whole-tree reformat commit is listed in the repo-root `.git-blame-ignore-revs` (`git config blame.ignoreRevsFile .git-blame-ignore-revs`).

## Hardening specs

After the specs pass, use the `mutation-fix` skill (`.claude/skills/mutation-fix/SKILL.md`) to run Stryker on the changed files and kill surviving mutants. Stryker disables (`// Stryker disable next-line <Mutator>: <reason>`) are only for genuinely equivalent mutants.
