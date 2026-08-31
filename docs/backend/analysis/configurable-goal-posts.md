# Configurable Goal Posts for Progress

Status: Planned, implementation starting. Extends `Features/Progress/` (see
[gamified-progress.md](gamified-progress.md) for the base feature this
builds on) with the first guardian *write* action on Progress.

## Context

`gamified-progress.md` shipped Progress with two hardcoded scales:

- A fixed milestone threshold list, `[5, 10, 25, 50, 100]`
  (`RecordStarChangeHandler.cs`).
- A fixed 4-stage icon sequence, `['🌱', '🌿', '🪴', '🌳']`
  (`progress-badge.ts`), clamped so the badge freezes on `🌳` once
  `unlockedMilestones().length >= 4`.

Both were flagged in their own comments as sketch-only ("no per-child
configuration yet"). In practice this means a child who completes tasks
consistently reaches 100 stars — and a visually maxed-out badge — in a few
weeks, after which nothing about their progress ever changes again. For the
app's stated audience (children with ADHD, per `gamified-progress.md`'s own
framing), a plateau is worse than no gamification: the whole point was
visible, ongoing feedback.

This document covers two things: how guardians configure the goal-post
scale per child, and how the system keeps generating goals indefinitely
without requiring a guardian to keep adding entries forever.

## Question 1: does configuration replace the hardcoded scale, or layer on top of it?

**Decision: guardian-configured goal posts live on the same `ChildProgress`
aggregate/stream as the facts they describe, as an optional overlay. An
unconfigured child (no `GoalPostsConfigured` event ever appended) falls back
to today's `[5, 10, 25, 50, 100]` + growth-stage-icon behavior exactly as
it exists today.**

This mirrors the codebase's existing "config lives on the aggregate it
describes" pattern — `Calendar.Icon`/`UpdateCalendarIcon` and
`MedicineSchedule`'s name/icon/color are both guardian-settable fields on
the same stream as the thing they configure, not a separate aggregate. It
also means zero migration: existing `ChildProgress` streams simply don't
have the new event yet, and `Rehydrate` folds an empty `GoalPosts` list,
which the handler already treats as "use the default."

A separate aggregate (e.g. a `ProgressGoalConfig` 1:1 with the child) was
considered and rejected for the same reason `gamified-progress.md` rejected
a separate index document for `ChildProgress` itself: a genuine 1:1
relationship doesn't need a second store or a lookup mechanism.

## Question 2: how does a small guardian-configured list produce goals forever?

**Decision: guardians configure an ordered, ascending list of
`GoalPost(Threshold, Icon, Label?)` — typically 3-6 entries. Once a child's
star count passes the last configured threshold, the system keeps
generating goal posts automatically: it repeats the *step* between the last
two configured thresholds, and cycles back through the configured icon
list, appending a round indicator (e.g. `🌱 ×2`) so a second pass is
visually distinct from the first.**

Concretely, given posts sorted ascending by threshold, with `step = last.Threshold
- secondToLast.Threshold` (guarded to be `> 0`; a single-post config uses
that post's own threshold as the step):

```
round 0: guardian's own posts, thresholds T0 < T1 < ... < Tn, icons I0..In
round k (k >= 1): threshold Tn + k * step, icon I(index % (n+1)), label suffixed " ×(k+1)" on the icon only
```

This was chosen over two alternatives:

- **Requiring guardians to keep extending the list manually** — rejected,
  because it re-creates the exact plateau problem this feature exists to
  fix; a guardian who configures 5 posts and forgets to add a 6th produces
  the same dead-end the hardcoded array already has.
- **An open-ended mathematical curve** (e.g. exponential spacing) with no
  guardian-authored icons at all past the configured range — rejected,
  because it removes the one thing guardians explicitly asked to control
  (icons), leaving them with thresholds only.

Extrapolation, not more configuration, is what makes the scale open-ended;
guardian configuration is what keeps every visible goal on the near-term
path meaningful and their own choice of icon.

### Where extrapolation is computed

**Decision: server-side, in a single shared resolver used by both the write
path (`RecordStarChangeHandler`, to decide whether a `MilestoneUnlocked`
threshold was just crossed) and the read path (`GetChildProgress`/
`GetMyProgress`, to resolve the child's current icon and next goal for
display).** Computing it twice (once in C#, once in TypeScript) would let
the two drift; the frontend stays a thin renderer of whatever the backend
resolves, consistent with how little business logic exists in the Angular
layer today (`progress-badge.ts` currently *does* own this logic, which is
exactly the duplication this decision removes).

## Question 3: who can write goal posts?

**Decision: active guardians only, never the child themself — the inverse
of `GetChildProgress`'s existing self-or-guardian read check.** A child
picking their own goal posts/icons would undermine the guardian's ability
to calibrate pacing (e.g. a younger child needing closer-together goals),
and no other guardian-configured setting in this codebase (medicine
schedules, calendars, pickup times) is writable by the child it concerns.
This is Progress's first write endpoint, so `GetChildProgressHandler`'s own
comment ("no guardian write action yet") is now out of date once this
ships.

## Question 4: per-child or per-family default?

**Decision: per-child**, matching `MedicineSchedule`'s existing per-child
configuration shape rather than introducing a family-level default with
per-child override — the latter would be a second config surface and an
inheritance rule this codebase has no precedent for anywhere. A guardian
with multiple children configures each child's goal posts separately, the
same way they already configure each child's medicine schedules
separately.

## Decisions made

| Question | Decision |
|---|---|
| Where does goal-post config live | Same `ChildProgress` aggregate/stream, new `GoalPostsConfigured` event, full-replace semantics |
| Unconfigured child behavior | Falls back to the original hardcoded `[5,10,25,50,100]` + 4-icon scale — zero migration |
| How progress stays visible past the configured range | Automatic extrapolation: repeat the last-two-posts' step, cycle icons, append a round indicator |
| Where extrapolation is computed | Server-side, one shared resolver, used by both the write path (milestone crossing) and read path (display) |
| Who can configure goal posts | Active guardians only, never the child |
| Config scope | Per-child, not per-family |

## Open questions not settled here

- Should a guardian be able to see/preview the extrapolated (round ≥ 1)
  posts before they're reached, or only the configured ones? (Leaning
  toward: yes, computed the same way, so the guardian can sanity-check
  pacing far in advance — not settled.)
- Should changing goal posts after a child has already unlocked some
  milestones retroactively re-evaluate `UnlockedMilestones`, or only affect
  future crossings? Current lean: only future crossings — `MilestoneUnlocked`
  is an append-only fact about what already happened, and rewriting history
  on a config change would be surprising. Not settled.
- Icon input remains a free-text emoji string, matching `Icon.cs` and every
  other icon field in this codebase (no picker widget exists anywhere yet).
  Whether a curated quick-pick palette is worth adding later is a UX
  question, not a data-model one, and is deferred.
</content>
