# Validation Rules

Status: Implemented

Both tracks below were adopted together rather than Track A alone: the team
chose to adopt FluentValidation for command validation *and* build the
structured error envelope, combined into one coherent change (see the
implementation plan this analysis fed into, summarized in the "Recommendation"
section's Track A/Track B split — both were done).

## Context

The question: does this codebase need a dedicated validation framework
(FluentValidation was the specific candidate), and if so, what would adopting
it take? This document is the audit behind that question, plus a
recommendation and an implementation plan for whichever direction is chosen.

## Current state

There is no `Domain`/`Application`/`Infrastructure` split — `buddy` is
organized as vertical slices per feature
(`Features/{Calendars,Groups,Guardians,Mealplans,Medicines,Pickups,Progress,Users}`),
each with one folder per use case containing a `*.Command.cs`,
`*.Endpoint.cs`, `*.Handler.cs` triad. 104 such use cases exist today.
Persistence is Marten (event sourcing over Postgres), not EF Core; mediation
is WolverineFx (`IMessageBus.InvokeAsync`), not MediatR.

Validation failures are never exceptions. Every handler returns a
`Result<T>` union ([Result.cs](../../../src/backend/buddy/Common/Result.cs))
or a feature-specific outcome union with the same shape, and a validation
failure is `return new Result<T>.Validation("message");` — a plain string,
not a structured object. Every endpoint's `switch` maps
`Validation(var message) => TypedResults.BadRequest(message)` the same way,
in roughly 40 endpoint files. No data annotations, no FluentValidation, no
other validation library, and no central exception-handling middleware exist
anywhere in the project (`Program.cs` has no `UseExceptionHandler`/
`ProblemDetails`).

Two validation sub-patterns recur, split with no stated rule for which check
goes where:

- **Endpoint-layer required-field checks** — `string.IsNullOrWhiteSpace(...)`
  guards that return `TypedResults.BadRequest(...)` before the command is
  even dispatched, e.g.
  [CreateChild.Endpoint.cs:21-26](../../../src/backend/buddy/Features/Guardians/CreateChild/CreateChild.Endpoint.cs),
  [UpdateEmail.Endpoint.cs:21-24](../../../src/backend/buddy/Features/Users/UpdateEmail/UpdateEmail.Endpoint.cs).
- **Handler-layer business-rule / cross-field / async checks** — run inside
  `Handle`, returning `Result<T>.Validation`. Examples across features:
  - Non-blank-name checks, the same three lines copy-pasted across features:
    [CreateMedicineSchedule.Handler.cs:16-19](../../../src/backend/buddy/Features/Medicines/CreateMedicineSchedule/CreateMedicineSchedule.Handler.cs),
    [CreateMeal.Handler.cs:16-19](../../../src/backend/buddy/Features/Mealplans/CreateMeal/CreateMeal.Handler.cs), and others.
  - Numeric range:
    [RateMeal.Handler.cs:14-17](../../../src/backend/buddy/Features/Mealplans/RateMeal/RateMeal.Handler.cs)
    (`Stars` 1-5).
  - Format/lookup against a fixed set:
    [CreateCalendar.Handler.cs:10-13](../../../src/backend/buddy/Features/Calendars/CreateCalendar/CreateCalendar.Handler.cs)
    (`TimeZoneResolution.IsValid`), `SupportedLanguages.IsValid`.
  - Enum-conditional required fields:
    [AssignPickup.Handler.cs ValidateFields](../../../src/backend/buddy/Features/Pickups/AssignPickup/AssignPickup.Handler.cs)
    — a switch over `PickupAssigneeKind` deciding which fields must be set.
  - Async, DB-backed relationship checks: the same handler's
    `ValidateRelationshipAsync` — is `GuardianId` actually an active guardian
    of this child, does `SiblingChildId` actually share a guardian with
    `ChildId`.
  - Cross-field date-range checks, duplicated near-identically four times:
    `ListOccurrences`, `ListTodaysDoses` (+`ForGroup`), `ListMealPlan`
    (+`ForGroup`), `ListPickupSchedule` — same `To < From` /
    `MaxRangeDays` (366) logic and message text, hand-copied per feature.
  - Rate-limit-as-validation, also duplicated: a 1-minute invite-resend
    cooldown in both `InviteToGroup.Handler.cs` and
    `InviteGuardian.Handler.cs`, whose comment explicitly says it "mirrors"
    the other.

Domain-level invariant enforcement is deliberately split by design, not by
accident. Strongly-typed IDs (`GroupId`, etc.) are unvalidated wrappers.
[TimeZoneId.cs](../../../src/backend/buddy/Features/Calendars/Types/TimeZoneId.cs)
says outright that it's kept a "plain data holder" and validity is checked in
handlers instead. `Period.TryCreate` ([Period.cs:29-45](../../../src/backend/buddy/Features/Calendars/Types/Period.cs))
is the exception — it does enforce `EndsAt > StartsAt` at construction, via a
`TryCreate` factory rather than a throwing constructor.

Two things are conspicuously missing everywhere: no string length limits on
any free-text field (`Name`, `Title`, `Comment`, ...), and no email format
check — `UpdateEmail` only checks the string is non-blank, not that it looks
like an email.

[http-status-codes.md](../http-status-codes.md#422-unprocessable-content) flags an
explicitly unresolved team decision: 400 vs. 422 for validation failures
("Team rule: choose one style ... and apply consistently" — not yet done;
code uniformly uses 400 today). It also recommends a structured
`{code, message, details, requestId}` error envelope that isn't implemented
— endpoints return a bare string body.

[pickup-schedules.md:117-150](pickup-schedules.md) documents a deliberate
architectural stance relevant to this decision: cross-feature reuse of
validation/business-rule logic is treated as a boundary violation to avoid.
`AssignPickup`'s sibling check duplicates guardian-lookup logic that already
exists in `Mealplans.MealFamilyResolution`, on purpose, "not before" a third
feature needs it.

## Question 1: is a validation framework necessary?

The rule *shapes* present (required, range, fixed-set lookup, cross-field,
async DB-relationship) are exactly what frameworks like FluentValidation are
built for. But the current pain isn't "the mechanism is broken" — every
validation failure already funnels through one consistent path
(`Result<T>.Validation` → `BadRequest`). The concrete problems are narrower:

1. Two real coverage gaps: no length limits, no email format check.
2. Duplicated logic *within the same technical shape* across features
   (date-range, resend cooldown, non-blank-name) — copy-paste, not a missing
   abstraction layer.
3. An inconsistent line between endpoint-layer and handler-layer checks.
4. A structured error response that's already been decided as the target
   (in `http-status-codes.md`) but never built.

None of those require a new framework — they're a scoped cleanup. A
framework would be a solution in search of a slightly bigger problem than
this codebase currently has at 104 use cases, most with zero or one rule.

## Question 2: if one were introduced, is FluentValidation a good fit?

**Where it fits well:** the rule shapes match its DSL almost one-to-one —
`NotEmpty()`/`MaximumLength()` for the required-string checks, `Must`/
`InclusiveBetween` for `RateMeal`'s range check, `MustAsync` for
`AssignPickup`'s guardian/sibling relationship checks, `When(...)` for its
enum-conditional required fields.

**Where it fights this codebase's specific conventions:**

1. **No exceptions.** FluentValidation's idiomatic integrations (a MediatR
   pipeline behavior, or Wolverine's own FluentValidation extension) work by
   throwing on failure. This codebase deliberately has no exception-based
   control flow for validation anywhere — everything is a `Result<T>` union.
   Using FluentValidation without fighting that means calling
   `validator.ValidateAsync(command)` manually inside each handler and
   adapting `ValidationResult.Errors` back into
   `Result<T>.Validation(string)` — which is the same amount of code
   currently in the `if` checks, just relocated behind a fluent builder.
2. **Wrong mediator, and no pipeline seam.** This app uses WolverineFx via
   direct `IMessageBus.InvokeAsync` calls from Minimal API endpoints, not
   WolverineFx.Http. There's no existing cross-cutting seam (comparable to a
   MediatR pipeline behavior) to hang automatic validation on without adding
   one — and Wolverine's own FluentValidation extension has the same
   throw/catch friction as point 1.
3. **Its headline value doesn't transfer.** FluentValidation's biggest win
   is shared, composable, testable rule objects reused across a codebase.
   This codebase's own docs (`pickup-schedules.md`) explicitly reject that
   kind of cross-feature reuse for business rules, on purpose, to preserve
   vertical-slice boundaries. Most of what's actually duplicated here
   (date-range, resend cooldown, non-blank-name) is generic *technical*
   validation, not a domain concept — it doesn't need FluentValidation's
   cross-cutting machinery to be shared; a plain static helper is enough.
4. **Single-message outcomes.** `Result<T>.Validation` and the per-feature
   outcome unions carry one `string`, not a list. Adopting FluentValidation
   without also widening that type loses its "collect every failing rule,
   not just the first" benefit — `string.Join` would flatten it back to one
   message anyway.

**Net:** FluentValidation is a reasonable match for the rule shapes, but a
mediocre match for this codebase's actual conventions (no exceptions, no
MediatR/Wolverine pipeline in use, and a deliberate anti-cross-feature-reuse
stance that removes its main advantage). It would be usable as a manually-
invoked rule composer for the two or three richest handlers
(`AssignPickup`, `CreateItem`), but that's a readability nicety, not a fix
for the actual gaps.

## Recommendation

**Track A — do this regardless (no new dependency, low risk):**

1. Add the two missing checks: max-length caps on free-text fields, and an
   email format check in `UpdateEmail`.
2. Extract the duplicated *technical* (non-domain) checks into small shared
   helpers — a `DateRange` check, a non-blank/max-length string check, a
   resend-cooldown check — likely under `Common/Validation/`. This is safe
   to centralize precisely because it isn't a domain concept (unlike the
   guardian/sibling lookups `pickup-schedules.md` deliberately keeps
   feature-local).
3. Resolve the open 400-vs-422 question and implement the structured
   `{code, message, details, requestId}` error envelope
   `http-status-codes.md` already recommends. This is the highest-leverage
   item here: it's already team-endorsed, just not built, and touches every
   endpoint's `Validation` mapping either way once decided.
4. Pick and document a rule for endpoint-layer vs. handler-layer checks
   (today's split has no stated reason), and make `ListEvents`'s page-size
   clamp consistent with its cursor's reject-don't-clamp behavior.

**Track B — only if the team wants a declarative framework for its own sake**
(e.g. anticipating substantially more use cases, or wanting testable rule
objects independent of handlers): add FluentValidation, invoked manually per
handler (skip the auto-throwing Wolverine extension), with a one-line
adapter: `Result<T>.Validation(string.Join("; ", result.Errors.Select(e =>
e.ErrorMessage)))`. This does not remove the need for Track A — it only
changes how the existing per-handler checks are written.

Track A is the recommended path: it closes the actual gaps, removes the
actual duplication, and finishes a decision the team already made on paper
— all without adding a dependency that fights three of this codebase's
existing conventions at once.

## Implementation plan (Track A)

1. `Common/Validation/DateRangeValidation.cs` — pure function
   `(DateOnly from, DateOnly to, int maxRangeDays) -> string?` (or a small
   result type), used by `ListOccurrences`, `ListTodaysDoses` (+`ForGroup`),
   `ListMealPlan` (+`ForGroup`), `ListPickupSchedule`.
2. `Common/Validation/RequiredText.cs` — non-blank + max-length check, used
   by `CreateChild`, `UpdateEmail`, `CreateMedicineSchedule`,
   `UpdateMedicineDetails` (×variants), `CreateMeal` (×variants),
   `UpdateMealDetails` (×variants), `UpdateChildLanguage`,
   `UpdateChildTimeZone`, `UpdateTimeZone`, `UpdateLanguage`. Needs a product
   decision on the actual length caps per field (name-like fields vs.
   comment-like fields).
3. Add an email format check to `UpdateEmail.Handler.cs` (a
   `System.Net.Mail.MailAddress`-based check is enough; no need for a regex
   library).
4. Extract the resend-cooldown constant + check into one shared helper used
   by `InviteToGroup.Handler.cs` and `InviteGuardian.Handler.cs`.
5. Decide 400 vs. 422 (recommend keeping 400 uniformly — it's what's already
   implemented and is the more common REST convention — and formally closing
   the open question in `http-status-codes.md`), then implement the
   structured error envelope and update all ~40 endpoints' `Validation`
   mapping plus `NotFound`/`Forbidden` mappings for consistency.
6. Add/extend integration tests in `buddy.IntegrationTests` for the new
   length/email checks and the de-duplicated helpers.

Step 5 is the largest — it touches most endpoint files — and is worth doing
as its own change separate from steps 1-4, since it's a cross-cutting
response-shape decision rather than a validation-rule fix.
