# In-app help for guardians

Status: Proposed (not yet implemented)

## Goal

As a guardian, I can open a short explanation of the page I'm on, written in my language, and I
can browse every help topic in one place. I shouldn't need to read the GitHub README or ask the
family's technical member what a "group-owned calendar" or a "goal post" is.

## Context and precedents

Buddy has no help today. Each page explains itself with one- or two-sentence i18n strings:

- `intro`: [sleep-diary.ts:5](../../../src/frontend/buddy/src/app/core/i18n/translations/en/sleep-diary.ts),
  `babysitters.ts:5`, `onboarding.ts:4`.
- `hint`: `progress.ts:7`, `admin.ts:37`.
- `help`: [sleep-diary.ts:62,70](../../../src/frontend/buddy/src/app/core/i18n/translations/en/sleep-diary.ts).
- `description`: `admin.ts:192-252`.

These stay where they are. They explain a single field or section. The help system explains a
whole page: what the page is for, how its concepts relate (group vs. child vs. calendar ownership,
`Manage` vs. `View`), and the usual steps.

Precedents this plan follows:

- **[Guardian onboarding](guardian-onboarding.md)** chose "not a demo, a tooltip tour, or a second
  family domain model". The help system makes the same choice. It is reference text read on
  demand. It doesn't walk the user through anything or create data.
- **No modals.** [`pickup-cell.ts:37-39`](../../../src/frontend/buddy/src/app/features/guardian/pickup/pickup-cell/pickup-cell.ts)
  and the [visual specification](visual-specification.md) both say "inline edit-in-place, no
  modals". The only dialog is the delete-account confirmation, built by hand. Help therefore
  expands inline. It is not an overlay.
- **Typed dictionaries.** `da` is typed against `typeof en` in
  [`translations/index.ts`](../../../src/frontend/buddy/src/app/core/i18n/translations/index.ts),
  so a missing Danish key fails the build. Help text gets the same guarantee.
- **Coverage specs.** [`screenshot-coverage.spec.ts`](../../../src/frontend/buddy/src/app/screenshot-coverage.spec.ts)
  walks the route config and fails when a page has no screenshot. A sibling spec can enforce
  "every guardian page has help" the same way.
- **Per-family hosting.** Each family runs its own instance, and a technical member operates it.
  Operator documentation (deploy, logs, privacy) stays in the repo's markdown. In-app help is only
  for using the app.

This document answers six questions: what form help takes, who gets it, where the text lives, how a
page finds its topic, whether the backend is involved, and how it's kept complete.

## Decision 1: a per-page help panel plus a help index

**Decision: a "?" button in the guardian shell header toggles an inline help panel for the
current page. A new `/guardian/help` page lists every topic, and the profile menu links to it.**

- The panel renders between the shell header and `<router-outlet />` in
  [`guardian-shell.html`](../../../src/frontend/buddy/src/app/features/guardian/shell/guardian-shell.html).
  It pushes the page content down instead of covering it, the same as an inline edit.
  - The button is a disclosure: `aria-expanded`, `aria-controls` pointing at the panel, and a
    `shell.help.toggle` label.
  - Escape closes the panel and returns focus to the button, as `ProfileMenu` does.
  - The panel closes when the user navigates to another page.
- The panel shows the topic's sections and ends with an "All help topics" link to
  `/guardian/help?topic=<id>`.
- `/guardian/help` shows a table of contents and then every topic in full.
  - `?topic=` scrolls the matching topic into view after the first render.
  - The router has no `withInMemoryScrolling`
    ([`app.config.ts:19`](../../../src/frontend/buddy/src/app/app.config.ts)), so `#fragment`
    links wouldn't scroll. The page scrolls itself instead of turning that on for the whole app.
- The profile menu gets a "Help" link
  ([`profile-menu.html`](../../../src/frontend/buddy/src/app/features/guardian/shell/profile-menu/profile-menu.html)),
  placed after the feature links and before "Sign out".

Considered and rejected:

- **Index page only.** People look for help on the page where they got stuck. Making them leave
  the page loses that context.
- **Panels only.** A topic like "groups and sharing" spans several pages. A new guardian also
  wants to read about features they haven't opened yet.
- **Field-level tooltips and popovers.** These need a new popover component, which breaks the
  no-overlay convention, and the existing `hint`/`help` strings already cover single fields.
  Listed under open questions.
- **A drawer or side sheet.** This is an overlay in all but name, and on an iPhone 15 (393px) it
  would cover the whole page.

## Decision 2: guardians only in v1

**Decision: children get no help system in v1. `/child` routes are not checked by the coverage
spec.**

The [visual specification](visual-specification.md) describes children as pre-readers. It asks for
sparse screens, one message at a time, and emoji as the main icons. A text panel is the opposite of
that. Child help is listed as an open question. If it's built, it would likely be emoji-led and
use different components.

## Decision 3: help text lives in typed `help.ts` dictionaries

**Decision: add a `help` area, `translations/{en,da}/help.ts`, alongside the 20 existing areas.
Topic structure lives in a typed TypeScript registry. The dictionaries hold only strings.**

`TranslationValue` is `string | { [k]: TranslationValue }`. There are no arrays, and
`TranslationService.translate` returns the key itself for anything that isn't a string. So the
order of sections and steps can't come from the dictionary. It comes from a registry:

```ts
// core/help/help-topics.ts
export interface HelpTopic {
  id: HelpTopicId;            // key under `help.topics`
  sections: readonly {
    id: string;               // help.topics.<id>.<section>.title / .body
    steps?: number;           // help.topics.<id>.<section>.steps.s1 .. sN, rendered as an <ol>
  }[];
  related?: readonly HelpTopicId[];
}

export const HELP_TOPICS: readonly HelpTopic[] = [
  { id: 'dashboard', sections: [{ id: 'overview' }, { id: 'cards' }] },
  { id: 'calendar', sections: [{ id: 'views' }, { id: 'ownership' }, { id: 'createEvent', steps: 4 }],
    related: ['groupsAndSharing'] },
  // ...
];
```

```ts
// translations/en/help.ts
export const help = {
  panelTitle: 'Help: {topic}',
  allTopics: 'All help topics',
  topics: {
    calendar: {
      title: 'Calendars',
      views: { title: 'Day, week and month', body: '...' },
      ownership: { title: 'Personal and group calendars', body: '...' },
      createEvent: { title: 'Add an event', body: '...', steps: { s1: '...', s2: '...', s3: '...', s4: '...' } },
    },
  },
};
```

- Bodies are plain text. Each `body` is one paragraph, and a topic with more to say gets another
  section. No markdown and no `innerHTML`, so there's nothing to sanitize.
- Wording follows the [glossary](../../backend/glossary.md) terms, but in user language: "group",
  "calendar shared with a group", and "can edit"/"can view" for `Manage`/`View`.
- The `i18n` skill's parity check and the `da`-typed-as-`en` build check cover the new area with
  no changes.

Considered and rejected:

- **Markdown files under `public/help/{en,da}/` fetched at runtime.** Long text would be easier to
  write, and a family's operator could edit it without rebuilding. But it adds a markdown
  renderer and HTML sanitizing (there's no renderer in `package.json` today), and it gives up the
  en/da parity check. Help would drift silently.
- **Reusing the `docs/screenshots` descriptions.** They exist in English only, they're one line
  each, and they're written for a developer reading the README.
- **Embedding screenshots in help.** `public/` holds no images today. The PNGs show English UI with
  demo data, and a Danish user would see the wrong language. Not in v1.

## Decision 4: a page names its topic in route data

**Decision: each guardian route declares `data: { helpTopic: '<id>' }` in
[`guardian.routes.ts`](../../../src/frontend/buddy/src/app/features/guardian/guardian.routes.ts).
`GuardianShell` reads the deepest active route's data and shows the "?" button only when a topic
is set.**

Before ([`guardian.routes.ts:38`](../../../src/frontend/buddy/src/app/features/guardian/guardian.routes.ts)):

```ts
{ path: 'sleep-diary', component: GuardianSleepDiary },
```

After:

```ts
{ path: 'sleep-diary', component: GuardianSleepDiary, data: { helpTopic: 'sleepDiary' } },
```

Blast radius: 15 one-line route edits, plus one new route (`help`). No page component or page
template changes. `GuardianShell` changes from an empty class to one that injects `Router` and
derives a `helpTopic` signal from `NavigationEnd` (or `toSignal` over router events).

Several routes can share a topic. `mealplan`, `mealplan/ai-assistant` and `mealplan/import` might
each get their own topic or share `mealPlans`; that gets settled while writing the content.
`onboarding` gets no topic because the guide explains itself.

Considered and rejected:

- **A `<app-page-help topic="...">` element in each page header.** That touches 15 page templates
  and specs instead of one shell. Each page's header markup differs (eyebrow + `h1` block, some
  with actions), and the button would end up in a different place on each page.
- **Deriving the topic from the URL.** This breaks on `print/templates/:templateId`, and when
  several routes share a topic it hides that in string matching.

## Decision 5: frontend only, nothing stored

**Decision: no backend changes. Help opens only on demand and never opens by itself, so there is
no "seen" or "dismissed" state to store, either in `localStorage` or on the server.**

Showing help automatically on a first visit would need per-user state, like
`OnboardingProgressDocument` in the Users store. It would also compete with onboarding for a new
guardian's attention. If it's wanted later, it's additive: a document with a set of dismissed
topic ids behind `GET`/`PUT /users/me/help`.

## Decision 6: completeness is enforced by a spec

**Decision: add `src/app/help-coverage.spec.ts`. It walks the guardian route tree the same way as
`screenshot-coverage.spec.ts` and checks four things:**

1. Every guardian page route inside the shell has a `helpTopic`, or a reason in a
   `ROUTES_WITHOUT_HELP` record (`/guardian/onboarding`, `/guardian/help`).
2. Every `helpTopic` used in routes exists in `HELP_TOPICS`.
3. Every key the registry implies (`title`, each section's `title`/`body`, `steps.s1..sN`)
   resolves to a string in `en`. Parity already guarantees `da`.
4. No key under `help.topics` is left out of the registry, so no orphan text.

If a new guardian page ships without help, `task test` fails. That's the same lever that keeps
screenshots complete.

## Frontend plan

| File | Change |
|---|---|
| `core/help/help-topics.ts` | New: `HelpTopicId`, `HelpTopic`, `HELP_TOPICS`, and `helpKeys(topic)`, which yields every key for the template and the coverage spec |
| `shared/help-content/help-content.{ts,html,spec.ts}` | New: renders one topic's sections (`h3` + `p` + optional `ol`). The panel and the index both use it |
| `features/guardian/shell/help-panel/help-panel.{ts,html,spec.ts}` | New: the inline panel (emerald card styling, like `OnboardingResumeCard`), its close button and the "All help topics" link |
| `features/guardian/shell/guardian-shell.{ts,html,spec.ts}` | Header "?" disclosure button next to `<app-profile-menu />`; `helpTopic`/`helpOpen` signals; closes on navigation |
| `features/guardian/help/help-page.{ts,html,spec.ts}` | New `/guardian/help`: table of contents, all topics, and `?topic=` scroll via `afterNextRender` |
| `features/guardian/guardian.routes.ts` | `data.helpTopic` on 15 routes; new `help` route |
| `features/guardian/shell/profile-menu/profile-menu.html` | "Help" link |
| `core/i18n/translations/{en,da}/help.ts` + `index.ts` | New area; `shell.help.toggle`/`close` and `shell.menu.help` keys in `shell.ts` |
| `src/app/help-coverage.spec.ts` | New, see Decision 6 |

Use the `buddy-frontend` skill for component and spec conventions, and the `i18n` skill for the new
area and parity.

### Topics (first cut)

`dashboard`, `calendar`, `taskLibrary`, `mealPlans` (planner, AI assistant, import, iCal links),
`medicine`, `sleepDiary` (including share links), `progress` (goal posts), `pickup`, `babysitters`,
`workLocations`, `print`, `admin` (profile, children, groups, sharing and permissions, AI
settings, data export and erasure), and a cross-cutting `groupsAndSharing` that the calendar, meal
plan and admin topics link to as `related`.

### Content guidelines

- Write from the guardian's task ("Add a medicine dose time"), not from the screen's layout.
- Keep each section to one paragraph, around 60 words at most.
- Don't promise behavior the code doesn't have. Check each claim against the flow docs
  (`docs/backend/<feature>/flow.md`) as it's written.
- Write the Danish yourself. Don't machine-translate it word for word. Use the same terms as the
  existing `da` areas.

## Testing

- Unit specs:
  - `help-content` renders sections and steps in registry order.
  - `help-panel`: close button, "All help topics" link with the `topic` query param.
  - `guardian-shell`: the button is hidden when the route has no topic, `aria-expanded` toggles,
    Escape closes the panel and focus returns to the button, and the panel closes on navigation.
  - `help-page`: the table of contents lists every topic, and `?topic=` scrolls to that topic.
- `help-coverage.spec.ts` (Decision 6).
- e2e: one spec, `e2e/help.spec.ts`. The guardian opens the sleep diary, opens help, follows "All
  help topics" and lands on that topic. Then they switch the language to Danish and the panel shows
  Danish text.
- Screenshots: add `guardian-help` (`/guardian/help`) to
  [`screenshots/pages.ts`](../../../src/frontend/buddy/screenshots/pages.ts). The coverage spec
  requires it because it's a new route. Optionally also capture `guardian-calendar-help`, the
  calendar page with the panel open, so the docs show the panel. This needs a small capture hook
  that clicks the button, because a `path` alone can't open it.

## Explicitly out of scope for this phase

- Help for children (Decision 2).
- Search across topics. With about 14 topics, the table of contents is enough.
- Tooltips, popovers or guided tours on individual fields.
- Screenshots or images inside help.
- Operator documentation (deploy, backups, logs, privacy duties). It stays in the repo markdown
  for the family's technical member.
- Backend state and auto-showing help on a first visit (Decision 5).

## Decisions made

| Question | Decision |
|---|---|
| Form | Inline per-page panel toggled from the shell header, plus `/guardian/help`; no modal or drawer, following the no-overlay convention |
| Audience | Guardians only; child screens are for pre-readers |
| Content storage | Typed `help.ts` dictionaries plus a TS registry for order, so en/da parity is enforced at build time |
| Page to topic mapping | `data.helpTopic` on routes, read by `GuardianShell`; one shell change instead of 15 page edits |
| Backend | None; help opens only on demand |
| Completeness | `help-coverage.spec.ts`, modelled on `screenshot-coverage.spec.ts` |

## Remaining open questions

- **Child help later?** Lean: a single emoji-led "how this works" card on the child home, in the
  child tone, if children or guardians ask for it. It's additive and doesn't affect this design.
- **Contact or "report a problem" link in the index?** Lean: a footer line "Something not working?
  Ask whoever runs Buddy for your family", plus the existing `runtimeConfig.repositoryUrl` link.
  This fits per-family hosting. A feedback form would need a backend and a recipient, so no.
- **Restart the guided setup from help?** `OnboardingResumeCard` only appears while the guide is
  Deferred. Lean: the dashboard topic links to `/guardian/onboarding` for a guardian who wants to
  look at it again. That page already handles a completed guide.
- **Meal-plan sub-pages: one topic or three?** Lean: one `mealPlans` topic with a section each for
  the AI assistant and import, so a guardian sees how they connect. Settle this while writing the
  content.
- **Field-level help later?** If the inline `hint`/`help` strings prove too terse, the next step is
  an expandable "More" link under the field (still inline), not a popover.
