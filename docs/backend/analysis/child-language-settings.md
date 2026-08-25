# Guardian-Managed Child Language

Status: Implemented

## Context

`Language` is already a first-class, self-service field on `User` — see
[Types/Language.cs](../../../src/backend/buddy/Features/Users/Types/Language.cs),
[SupportedLanguages.cs](../../../src/backend/buddy/Features/Users/SupportedLanguages.cs),
and the self-service `PATCH /users/me/language` endpoint
([UpdateLanguage.Handler.cs](../../../src/backend/buddy/Features/Users/UpdateLanguage/UpdateLanguage.Handler.cs)).
The frontend's hand-rolled i18n
([translation.service.ts](../../../src/frontend/buddy/src/app/core/i18n/translation.service.ts))
already applies whatever language is stored on a signed-in user's own `User`
record the moment they sign in, via
[users.service.ts](../../../src/frontend/buddy/src/app/core/users.service.ts)`.ensureCurrentUser()`.

The gap: every existing profile-update endpoint (`Name`, `Email`, `TimeZone`,
`Language`) is hard-wired to "self only" — each command is built via
`FromClaims(principal, …) => new(principal.GetUserId(), …)`. There is no
mechanism today for a guardian to change a value on a child's `User`. A
child (see
[child-accounts-and-guardian-roles.md](child-accounts-and-guardian-roles.md))
is just a `User` linked to a guardian via `GuardianLink`
([Types/GuardianLink.cs](../../../src/backend/buddy/Features/Guardians/Types/GuardianLink.cs)) —
young children in particular may not read well enough to navigate a
language picker themselves, so the parent needs to be able to set it for
them.

This document records three decisions and the resulting design.

## Decision 1: who can set it

**Any active guardian** (`GuardianKind.Parent` or `GuardianKind.Guardian`),
not just `Parent`-kind links.

This matches every existing precedent for "guardian acts on behalf of a
child": `GuardianKind` is a descriptive/record-keeping label only and never
gates access anywhere in the codebase today
([Types/GuardianKind.cs](../../../src/backend/buddy/Features/Guardians/Types/GuardianKind.cs)) —
`MedicineAuthorization.ResolveTier`
([MedicineAuthorization.cs](../../../src/backend/buddy/Features/Medicines/MedicineAuthorization.cs))
grants `Manage` to any active `GuardianLink` regardless of kind, and
`CalendarAuthorization`'s guardian-default-owner step does the same. Treating
language as the first field where `Kind` suddenly matters would be a new,
inconsistent rule with no stated need behind it.

## Decision 2: where the capability lives

**A new endpoint in the Guardians feature**, not an extension of the Users
feature's existing `PATCH /users/{userId}/language`.

The Guardians feature already depends on `IUserEventStore` directly —
`ListMyChildrenHandler`
([ListMyChildren.Handler.cs](../../../src/backend/buddy/Features/Guardians/ListMyChildren/ListMyChildren.Handler.cs))
reads a child's `User` stream to build each `ChildSummary` — and every other
guardian-acts-on-a-child action (`RevokeGuardianLink`, `InviteGuardian`,
`ListChildGuardians`) already lives here, keyed by `{childId}` under
`/users/me/children`. Adding "change a child's language" alongside them
keeps all guardian-on-behalf-of authorization logic in one place, rather
than teaching the Users feature's self-service endpoint to also understand
guardianship. The new handler reuses `Language`, `SupportedLanguages`, and
the existing `LanguageUpdated` event verbatim — no new event type, so
existing event-shape golden files and consumers (e.g. the frontend's
`TranslationService`) are unaffected.

## Decision 3: UI placement

**Inline on the existing `manage-children` screen** — a language `<select>`
added to each child's row
([manage-children.html](../../../src/frontend/buddy/src/app/features/guardian/admin/manage-children/manage-children.html)) —
rather than a new per-child settings page. `manage-children` is already the
guardian's one screen for child-scoped actions (add child, revoke, invite a
co-guardian); a single additional field doesn't justify new navigation, and
this keeps the change small. A dedicated per-child settings page can be
introduced later if more child-specific settings accumulate.

## Design

**Backend** — `Features/Guardians/UpdateChildLanguage/`:

- `UpdateChildLanguage(UserId? GuardianId, UserId ChildId, Language Language)`,
  built via `FromClaims(principal, childId, language)` — same shape as
  `RevokeGuardianLink.Command.cs`.
- Handler takes `IGuardianLinkEventStore` (to authorize) and `IUserEventStore`
  (to read/append to the child's stream), the same two stores
  `ListMyChildrenHandler` already injects:
  1. `guardians.FindActiveLinkAsync(childId, guardianId, …)` — `null` ⇒
     `Result.NotFound` (same "can't distinguish no-such-child from
     not-your-child" collapsing `RevokeGuardianLinkHandler` uses).
  2. `SupportedLanguages.IsValid(language)` ⇒ `Result.Validation` otherwise,
     identical check to `UpdateLanguageHandler`.
  3. Rehydrate the child's `User` via `users.ReadAsync(childId, …)`; no-op if
     `ResolvedLanguage` is already the requested value; otherwise append
     `LanguageUpdated(childId, before, after, now)` via
     `users.AppendAsync(childId, …)`.
- Route: `PATCH /users/me/children/{childId:guid}/language`, mapped in
  [GuardiansFeature.cs](../../../src/backend/buddy/Features/Guardians/GuardiansFeature.cs)
  next to `MapRevokeGuardianLink`.
- `ChildSummary`
  ([ListMyChildren.Handler.cs](../../../src/backend/buddy/Features/Guardians/ListMyChildren/ListMyChildren.Handler.cs))
  gains a `Language` field (the child's `ResolvedLanguage`) so the frontend
  can show the current value without a second round trip —
  `ListMyChildrenHandler` already has the rehydrated `User` in hand.

**Frontend**:

- `GuardiansService.ChildSummary` gains `language: string`; a new
  `updateChildLanguage(childId, language)` method calls the new endpoint,
  mirroring `UsersService.updateLanguage`.
- `ManageChildren` gains a per-child language `<select>` (options from the
  existing `SUPPORTED_LANGUAGES`/`LANGUAGE_NAMES` in
  [language.ts](../../../src/frontend/buddy/src/app/core/i18n/language.ts)),
  saving immediately on change (no separate save button, consistent with the
  screen's existing inline-action style) with a per-child saving/error signal
  keyed by child id, the same `Record<string, …>` pattern
  `invitesByChildId`/`invitesLoading` already use.
- New translation keys under `admin.manageChildren.language.*` in both
  `en/admin.ts` and `da/admin.ts`.

This does **not** touch the kid-facing app at all — it already applies
whatever language is stored on the child's `User` the moment the child signs
in, via the same `ensureCurrentUser()` → `setLanguageFromServer()` path every
account uses.

## Decisions made

| Question | Decision |
|---|---|
| Which guardians can set a child's language | Any active `GuardianLink`, regardless of `GuardianKind` — consistent with every other guardian-on-behalf-of-child action in the codebase |
| Where the endpoint/handler lives | Guardians feature (`/users/me/children/{childId}/language`), not an extension of the Users feature's self-service endpoint |
| New event type needed | No — reuses the existing `LanguageUpdated` event verbatim |
| Where the UI lives | Inline on the existing `manage-children` screen, not a new per-child settings page |
| Does `ChildSummary` need a `Language` field | Yes — added so the guardian UI can show/edit the current value without an extra request |

## Remaining open questions

- Whether the child should be notified (in-app or otherwise) when a guardian
  changes their language — out of scope here; today no profile field change
  notifies anyone, and this follows that precedent.
- Whether a per-child settings page is worth introducing once more
  child-specific settings exist beyond language — deferred until there's a
  second field that would live there.
