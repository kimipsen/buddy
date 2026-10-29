# Keep the screen awake while a child's timer runs — implementation plan

Status: Implemented. `core/screen-wake-lock.service.ts` (`ScreenWakeLockService`, a ref-counted
Screen Wake Lock that re-requests on `visibilitychange`) and `core/keep-awake.ts`
(`keepAwakeWhile`, with the 2-minute grace and the 4-minute token keep-alive), hooked into `/child`,
`/child/focus/:itemId`, `/child/homework` and `/child/first-then`, with unit specs and
`e2e/keep-screen-awake.spec.ts`. Frontend only. This is a follow-up to
[Visual time-left timer for the child](visual-time-left-timer.md), which built the run bar, the
focus view and the shared disc, and to
[Expired sessions during in-app navigation](expired-session-handling.md), which sends a child whose
session ended back to `/login`. Read it alongside
[`ChildTimerService`](../../../src/frontend/buddy/src/app/core/child-timer.service.ts),
[`visual-timer.ts`](../../../src/frontend/buddy/src/app/core/visual-timer.ts) and
[`AuthService`](../../../src/frontend/buddy/src/app/core/auth.service.ts).

## Goal

As a child, when I start a 20-minute "Then" timer or a 45-minute homework chunk on the iPad, the
screen stays on and the timer stays visible until it's done, and when it ends I'm still signed in.
As a guardian, I don't have to change the iPad's auto-lock setting or sign my child in again after
every long timer.

## What happens today

Four child screens show a counting-down timer, all built on `TimerState.phase` from
[`visual-timer.ts:22`](../../../src/frontend/buddy/src/app/core/visual-timer.ts):

| Screen                       | State signal                                                                                                          | Typical length             |
| ---------------------------- | --------------------------------------------------------------------------------------------------------------------- | -------------------------- |
| `/child` run bar             | `timer` ([home.ts:395](../../../src/frontend/buddy/src/app/features/child/home/home.ts))                              | a routine, 10-30 min       |
| `/child/focus/:itemId`       | `state` ([child-focus.ts:77](../../../src/frontend/buddy/src/app/features/child/focus/child-focus.ts))                | same run                   |
| `/child/homework`            | `state` ([child-homework.ts:148](../../../src/frontend/buddy/src/app/features/child/homework/child-homework.ts))      | 15-45 min chunks + breaks  |
| `/child/first-then/:boardId` | `timer` ([child-first-then.ts:74](../../../src/frontend/buddy/src/app/features/child/first-then/child-first-then.ts)) | `thenMinutes`, often 20-60 |

Two things go wrong on a long timer:

1. **The screen locks.** iPadOS auto-lock is 2-5 minutes by default. The timer keeps its anchor
   (`child-timer-storage.ts`, `homework-storage.ts`), so the countdown is right again after
   unlocking, but the child can't _see_ time passing, which is the whole point of a visual timer,
   and "Time's up" shows on a dark screen.
2. **The session ends.** Keycloak's defaults apply (access token 5 min, SSO Session Idle 30 min,
   SSO Session Max 10 h; see
   [expired-session-handling.md, Why a silent refresh can't fix it](expired-session-handling.md#why-a-silent-refresh-cant-fix-it)).
   The refresh token is only used when the app calls the API. While the screen is locked, JavaScript
   is suspended, so nothing calls; after 30 minutes the refresh token is dead and the child's next
   tap lands on `/login?reason=session-expired`. Even with the screen on, a timer page may make no
   API call for 30 minutes: the transition-warning banner's 10-minute poll
   ([child-warning-feed.service.ts:17](../../../src/frontend/buddy/src/app/core/child-warning-feed.service.ts))
   only runs when the `transitionWarnings` flag is on, and only the First-Then screens poll their
   board ([visible-poll.ts](../../../src/frontend/buddy/src/app/core/visible-poll.ts)).

## Scope decision (settled with the user, 2026-10-29)

- Frontend only. No backend, API contract, feature flag or Keycloak realm change.
- Keep the screen awake **only while a timer is running** (plus a short grace after "Time's up"),
  not whenever a timer page is open, and with no guardian setting.
- Keep the session alive **only while that wake lock is wanted**, by refreshing the token on a
  timer. Longer Keycloak lifetimes ("Remember me", a higher realm idle timeout) and storing tokens
  in `localStorage` were offered and not chosen; see [Rejected alternatives](#rejected-alternatives).

## Decisions

**Decision: use the Screen Wake Lock API, with no fallback.**

`navigator.wakeLock.request('screen')` is supported in Safari 16.4+ (including home-screen web
apps since iPadOS 18.4), Chrome/Edge 84+ and Firefox 126+, which covers the iPads and phones the
family uses ([ipad-installation.md](ipad-installation.md)). It needs a secure context: production
is HTTPS and `http://localhost:4300` counts as secure. Where the API is missing or the request is
refused (low battery mode on some devices), Buddy does nothing and the screen locks as today.
Rejected: the "NoSleep" trick of looping a silent hidden video, because it costs more battery,
breaks with autoplay policies and is exactly what the Wake Lock API replaced.

**Decision: the lock follows the countdown, not the disc.**

The lock is wanted while the screen's `TimerState.phase === 'running'`, whether the child pressed
Start (`childStarted`) or the run is schedule-anchored. `upcoming`, `paused`, `done` and
`timesUp` don't hold it. `paused` matters: a child who pauses and walks away should not leave a lit
iPad. The one exception is First-Then: its `timer` signal is `null` when "Show time-left timers" is
off ([child-first-then.ts:74-77](../../../src/frontend/buddy/src/app/features/child/first-then/child-first-then.ts)),
but the screen still shows "Time's up" when the Then ends, so the lock reads
`thenTimerState(board, now)` directly. On `/child` and the focus view, timers off means no run at
all, so no lock.

**Decision: hold it 2 minutes past "Time's up", then let the device lock.**

When the phase leaves `running`, release is delayed by `WAKE_LOCK_GRACE_MS = 2 * 60_000`, so the
child (or a guardian across the room) sees "Time's up". If the phase goes back to `running` inside
the grace (Keep going, the next homework step, a new routine step), the lock is kept without
re-requesting it.

**Decision: re-request on `visibilitychange`.**

The browser drops a wake lock whenever the page is hidden (app switch, manual lock button).
`ScreenWakeLockService` listens for `visibilitychange` once, for the whole app, and requests the
lock again when the page is visible and anyone still holds it, the same listener shape as
[visible-poll.ts](../../../src/frontend/buddy/src/app/core/visible-poll.ts).

**Decision: one root service, one per-component helper.**

`core/screen-wake-lock.service.ts` owns the single `WakeLockSentinel` and counts holders, so two
components that both want it (the run bar on `/child` and a future banner) can't release each
other's lock. Components don't call it directly; they call `keepAwakeWhile(signal)` once in their
constructor (injection context), which wires an `effect`, the grace timer, the keep-alive and
`DestroyRef` clean-up. Rejected: calling `request()`/`release()` from each component's
`ngOnInit`/`ngOnDestroy`, because four components would each re-implement the grace and the
visibility handling.

**Decision: while the lock is wanted, refresh the token every 4 minutes.**

`keepAwakeWhile` also starts `setInterval(() => void auth.getAccessToken(), SESSION_KEEP_ALIVE_MS)`
with `SESSION_KEEP_ALIVE_MS = 4 * 60_000`. `getAccessToken()` already returns the cached token
while it's fresh and refreshes it within `REFRESH_SKEW_MS` of expiry
([auth.service.ts:126](../../../src/frontend/buddy/src/app/core/auth.service.ts)), so most ticks
cost nothing and every second tick does one refresh (about every 8 minutes with the 5-minute access
token), which resets Keycloak's 30-minute idle clock well before it runs out. The keep-alive runs on the _wanted_ state, not on whether the lock was granted: on a device
without the API, a child who keeps tapping still keeps their session. It stops with the lock (after
the grace), so an iPad left on `/child` with no timer running still idles out after 30 minutes, as
today. A failed refresh calls `expireSession()` as today; the timer keeps counting locally and the
next navigation or API call goes to `/login`.

Rejected: a keep-alive on every child page regardless of timers. It would make a child's session
effectively endless on any iPad left open on Buddy, which is a security decision the user didn't
take; the timer-scoped version fixes the reported problem.

## Frontend plan

No visible UI change, so no mockup, no i18n keys and no screenshot changes.

### 1. `core/screen-wake-lock.service.ts` (new)

```ts
// The screen wake lock for the child's timers (docs/frontend/analysis/keep-screen-awake.md).
// Counts holders so one component releasing can't drop another's lock.
@Injectable({ providedIn: 'root' })
export class ScreenWakeLockService {
  private readonly document = inject(DOCUMENT);
  private holders = 0;
  private sentinel: WakeLockSentinel | null = null;

  constructor() {
    // Re-requests on visibilitychange (visible) while anyone holds it.
  }

  /** Takes the lock (if the browser allows) and returns the release function, safe to call twice. */
  hold(): () => void { ... }
}
```

Every `request()` is wrapped in `try`/`catch` and ignores a missing `navigator.wakeLock`.

### 2. `core/keep-awake.ts` (new)

```ts
export const WAKE_LOCK_GRACE_MS = 2 * 60_000;
export const SESSION_KEEP_ALIVE_MS = 4 * 60_000;

// Call in a constructor. Keeps the screen on, and the session alive, while `wanted()` is true and
// WAKE_LOCK_GRACE_MS after it turns false.
export function keepAwakeWhile(wanted: Signal<boolean>): void { ... }
```

### 3. The four timer screens

One line each in the constructor:

```ts
keepAwakeWhile(computed(() => this.timer()?.phase === 'running'));        // home.ts
keepAwakeWhile(computed(() => this.state()?.phase === 'running'));        // child-focus.ts, child-homework.ts
keepAwakeWhile(computed(() => {                                           // child-first-then.ts
  const board = this.board();
  return board !== null && thenTimerState(board, this.now())?.phase === 'running';
}));
```

`now` ticks every few seconds on each screen already, so `phase` flips to `timesUp` on time.

### 4. Testing

- `screen-wake-lock.service.spec.ts`: a stub `navigator.wakeLock`. One `request` for two holders;
  release only after the last; a double release counts once; a lock granted after everyone let go
  is released at once; `visibilitychange` to visible requests again only when the lock was dropped
  and someone holds it; no throw without the API, on a refused request or a failing release.
- `keep-awake.spec.ts` with fake timers: holds on `true`; still held at grace − 1 ms after `false`,
  released at grace; a flip back inside the grace doesn't re-request; `getAccessToken` called every
  `SESSION_KEEP_ALIVE_MS` while wanted and not after the grace; destroy releases and clears both
  timers.
- One case in each of the four component specs: running → service held; `upcoming`, `timesUp` or
  no current run → not held. The First-Then spec also covers "timers off, Then running → held".
  The First-Then "ticks only while counting down" case counts the keep-alive interval too.
- e2e: `e2e/keep-screen-awake.spec.ts` stubs `navigator.wakeLock` with `page.addInitScript` to
  record calls, has a guardian set a First – Then board, and checks that the child's page asks for
  no lock before Done and for one `request('screen')` once the Then counts down. First – Then
  rather than `/child/focus`, because a routine would have to be scheduled at the current minute.
  Headless Chromium may refuse a real lock, so the stub is required.

## Edge cases

| Case                                            | Behavior                                                                   |
| ----------------------------------------------- | -------------------------------------------------------------------------- |
| Browser has no Wake Lock API                    | No lock, no error; keep-alive still runs while a timer runs                |
| `request()` rejected (low power, permission)    | Swallowed; screen locks as today                                           |
| Child pauses                                    | Released after the 2-minute grace; keep-alive stops with it                |
| Child taps Keep going inside the grace          | Lock kept, no new request                                                  |
| Page hidden then shown while running            | Browser dropped the lock; re-requested on `visibilitychange`               |
| Child navigates from `/child` to the focus view | Old component releases on destroy, new one holds; the ref count bridges it |
| Screen locked anyway (no API) for > 30 min      | Session idles out as today; `/login?reason=session-expired` on next tap    |
| Keep-alive refresh fails                        | `expireSession()`; timer keeps counting; next request redirects            |
| Timer running for > 10 h (SSO Session Max)      | Not a realistic timer; the session ends as today                           |

## Decisions made

| Question                     | Decision                                                                                    |
| ---------------------------- | ------------------------------------------------------------------------------------------- |
| When is the screen kept on?  | While a timer's phase is `running`, plus a 2-minute grace after it stops (user, 2026-10-29) |
| How do sessions last longer? | A token keep-alive every 4 minutes for the same span; no Keycloak change (user, 2026-10-29) |
| Guardian setting?            | None; "only while running" limits the battery cost                                          |
| e2e                          | One spec on First – Then with a stubbed `navigator.wakeLock` (user, 2026-10-29)             |

## Rejected alternatives

- **Keycloak "Remember me"** (`rememberMe` plus `ssoSessionIdleTimeoutRememberMe` /
  `ssoSessionMaxLifespanRememberMe` in [buddy-realm.json](../../../.devcontainer/keycloak/buddy-realm.json)
  and prod's admin console). Would let a child iPad stay signed in for days, opt-in per login, with
  no code change: the `buddy` login theme only overrides `template.ftl`, so the stock checkbox
  would appear. Not chosen now; it stays the natural next step if children still get signed out
  between timers.
- **Raising SSO Session Idle realm-wide.** No opt-in, and it lengthens guardians' sessions on
  shared computers too. Already declined in
  [expired-session-handling.md](expired-session-handling.md#remaining-open-questions).
- **Tokens in `localStorage`.** Puts a long-lived refresh token where any script on the origin can
  read it, as the earlier doc said for `offline_access`.
- **A per-child guardian switch.** Needs a `User` field, an event and a `PATCH` slice like
  `ShowVisualTimersUpdated`; "only while running" already limits the battery cost.

## Explicitly out of scope for this phase

- Guardian pages (the print editor, the guardian First-Then page): no countdowns a guardian watches.
- The feelings check-in tick and the transition-warning banner: they don't hold the lock on their
  own; a warning countdown is short and appears on a page the child is already using.
- Any Keycloak realm change.

## Estimate

|                  |                                                                                                                                                        |
| ---------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------ |
| Complexity       | Low -- frontend-only: one root service, one helper with two timers and a visibility listener, a one-line hook in four child components, no i18n, no UI |
| Single developer | 1-1.5 days, including trying it on a real iPad in Safari and as a home-screen app                                                                      |
| AI agent         | 45-75 minutes, plus about 30 minutes of human review (leave an iPad on a 10-minute timer with auto-lock at 2 minutes)                                  |

## Verification

- `npx ng test --watch=false --include` for the two new specs and the four component specs, then
  `task test:frontend`.
- Manual (screen): on an iPad with auto-lock at 2 minutes, start a 5-minute First-Then "Then".
  The screen stays on through "Time's up" and dims about 2 minutes later. Pause a routine timer on
  `/child/focus`: the screen dims after the grace plus auto-lock.
- Manual (session): in the Keycloak admin console set SSO Session Idle to 10 minutes (realm
  `buddy` -> Realm settings -> Sessions; it must stay above the ~8 minutes between keep-alive
  refreshes), sign in as a child, start a 20-minute homework chunk and don't touch
  the screen. When it ends, tapping Next works without a trip to `/login`. Without a running timer,
  the same wait still ends on `/login?reason=session-expired`.
