# Expired sessions during in-app navigation — implementation plan

Status: Implemented. `AuthService.sessionExpired`/`expireSession()`, the interceptor's redirect
on an ended session or a 401, `authGuard` as `canActivate` + `canActivateChild` with a token
freshness check (same check in `roleRedirectGuard`), the `/login?reason=session-expired` notice, and
the return-to-page stand-in `pending-return-url.ts`. Keycloak's idle timeout was left at its
default. Read it alongside
[`AuthService`](../../../src/frontend/buddy/src/app/core/auth.service.ts),
[`authInterceptor`](../../../src/frontend/buddy/src/app/core/auth.interceptor.ts),
[`authGuard`](../../../src/frontend/buddy/src/app/core/auth.guard.ts) and
[`roleRedirectGuard`](../../../src/frontend/buddy/src/app/core/role.guard.ts).

## Goal

As a guardian or child who left Buddy idle long enough for the Keycloak session to end, the next
click inside the app takes me to the login page with a short "your session expired" note, instead
of leaving me on a half-rendered page full of load errors.

## What happens today

A browser reload already ends up on `/login`. In-app navigation does not:

1. `authGuard` is a `canActivate` on the `guardian` and `child` parents only
   ([app.routes.ts:41-50](../../../src/frontend/buddy/src/app/app.routes.ts)). Angular runs it when
   `/guardian` is first entered, never when moving between its children (`/guardian/calendar` ->
   `/guardian/mealplan`). Nothing checks the session on navigation.
2. The new page's first API call goes through `authInterceptor` -> `AuthService.getAccessToken()`.
   The access token is expired, so it tries a refresh. Keycloak rejects the refresh token because
   the SSO session has idled out, and `refreshAccessToken` clears the tokens and returns `null`
   ([auth.service.ts:122-145](../../../src/frontend/buddy/src/app/core/auth.service.ts)).
3. The interceptor sends the request anyway, without an `Authorization` header
   ([auth.interceptor.ts:18-20](../../../src/frontend/buddy/src/app/core/auth.interceptor.ts)). The
   API answers 401 and the page shows its load error. Nothing in the frontend handles 401 (no
   `status === 401` anywhere under `src/app`).
4. The tokens are gone from `sessionStorage` now, so a reload finds no tokens and `authGuard` sends
   the user to `/login`. That's why reload "works".

A related gap: `authGuard` and `roleRedirectGuard` test `auth.isAuthenticated()`, which only checks
that a token set *exists* ([auth.service.ts:24](../../../src/frontend/buddy/src/app/core/auth.service.ts)).
A reload with stale-but-present tokens (no failed call cleared them yet) passes the guard and
renders the error page once. The guard change below closes that case too.

## Why a silent refresh can't fix it

The dev realm ([buddy-realm.json](../../../.devcontainer/keycloak/buddy-realm.json)) sets no session
lifetimes, so Keycloak's defaults apply: access token 5 min, SSO Session Idle 30 min, SSO Session
Max 10 h. The refresh token lives exactly as long as the idle session. After 30 idle minutes there
is nothing left to refresh with: not the refresh token, and not a `prompt=none` re-authorization,
because the Keycloak SSO cookie session is gone as well. Under 30 minutes, the existing
`getAccessToken` refresh already works. Prod's realm was recreated by hand
([deploy/README.md](../../../deploy/README.md), section 5), so its lifetimes are whatever its admin
console says, most likely the same defaults.

So the frontend fix is to *detect* the ended session and send the user to log in. Making sessions
last longer is a separate Keycloak setting (see [Remaining open questions](#remaining-open-questions)).

## Scope decision

Frontend only. No backend or API change: the API's 401 for a missing or invalid bearer token is
already the right answer.

## Decisions

**Decision: redirect only when a session existed and ended, never for "no token at all".**

The interceptor can't treat every `null` token as "go log in": four routes call the API while
logged out on purpose — `/shared/sleep-diary/:token`, `/invite/:token`, `/guardian-invite/:token`
and `/verify-email/:token` ([app.routes.ts:17-40](../../../src/frontend/buddy/src/app/app.routes.ts)).
`AuthService` gets a `sessionExpired` signal that turns true only when a refresh fails or the API
rejects a token the app sent. It turns false again on a new token set. Rejected: redirecting on any
`null` token, because it would bounce a clinician off a sleep-diary share link.

**Decision: the interceptor stops the request and redirects, instead of sending it unauthenticated.**

When `getAccessToken()` returns `null` and `sessionExpired()` is true, there's no point making the
call: it can only 401. The interceptor navigates to `/login?reason=session-expired` and errors the
request with a synthetic 401 `HttpErrorResponse`, so every caller's existing error path still runs
(no hung promises, no `EmptyError` from `firstValueFrom`). A real 401 on a request that *did*
carry a token gets the same treatment, after calling `auth.expireSession()`. The redirect is
skipped when the router is already on `/login`, so a page firing six parallel calls navigates once.

**Decision: guards check token freshness and also run on child navigation.**

`authGuard` and `roleRedirectGuard` await `auth.getAccessToken()` instead of reading
`isAuthenticated()`. A dead session then lands on `/login` *before* the page renders, so the user
never sees the error flash. `authGuard` is also added as `canActivateChild` on `guardian` and
`child`, so it runs on every navigation inside those trees. That's cheap: `getAccessToken()` returns
the cached token without a network call while it's fresh, `completeLoginRedirect()` returns at once
without a `code` in the URL, and `ensureCurrentUser()` is memoized
([users.service.ts:45](../../../src/frontend/buddy/src/app/core/users.service.ts)). Angular runs a
route's `canActivate` and `canActivateChild` one after the other, not in parallel, so the race that
[role.guard.ts:13-18](../../../src/frontend/buddy/src/app/core/role.guard.ts) warns about doesn't
apply. Rejected: the interceptor alone. It works, but only after the page has rendered and started
loading.

**Decision: go to Buddy's `/login` page with a notice, not straight to Keycloak.**

Calling `auth.login()` directly would save a click, since Keycloak shows its form either way. But
the user wouldn't learn *why* they were logged out, and a child on an iPad would find themselves
on an unfamiliar Keycloak page. `/login` reads `reason=session-expired` from the query string and
shows one line above the sign-in button.

## Frontend plan

### 1. `core/auth.service.ts`

Add the signal and fold the two copies of the "clear session" code into one helper.

Today ([auth.service.ts:122-145](../../../src/frontend/buddy/src/app/core/auth.service.ts)):

```ts
private async refreshAccessToken(current: TokenSet): Promise<string | null> {
  if (!current.refreshToken) {
    this.tokens.set(null);
    clearStoredTokens(sessionStorage);
    return null;
  }
  try {
    // ...
  } catch {
    this.tokens.set(null);
    clearStoredTokens(sessionStorage);
    return null;
  }
}
```

Proposed:

```ts
private readonly sessionExpiredState = signal(false);
readonly sessionExpired = this.sessionExpiredState.asReadonly();

/** Ends a session the API or Keycloak rejected; the interceptor and guards send the user to /login. */
expireSession(): void {
  this.tokens.set(null);
  clearStoredTokens(sessionStorage);
  this.sessionExpiredState.set(true);
}

private async refreshAccessToken(current: TokenSet): Promise<string | null> {
  if (!current.refreshToken) {
    this.expireSession();
    return null;
  }
  try {
    // ...unchanged
  } catch {
    this.expireSession();
    return null;
  }
}

private setTokens(tokens: TokenSet): void {
  this.tokens.set(tokens);
  this.sessionExpiredState.set(false);
  writeStoredTokens(sessionStorage, tokens);
}
```

`logout()` keeps clearing without setting `sessionExpired`: a deliberate logout isn't an expiry.
Blast radius: one file. The `getAccessToken` specs in `auth.service.spec.ts` gain assertions on
the signal; no existing assertion changes.

### 2. `core/auth.interceptor.ts`

Today ([auth.interceptor.ts:16-24](../../../src/frontend/buddy/src/app/core/auth.interceptor.ts)):

```ts
return from(auth.getAccessToken()).pipe(
  switchMap((token) => {
    if (!token) {
      return next(req);
    }

    return next(req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }));
  }),
);
```

Proposed:

```ts
return from(auth.getAccessToken()).pipe(
  switchMap((token) => {
    if (!token) {
      // Anonymous pages (share links, invite previews) call the API with no session at all;
      // only a session that existed and ended goes back to /login.
      return auth.sessionExpired() ? sessionEnded(router, req) : next(req);
    }

    return next(req.clone({ setHeaders: { Authorization: `Bearer ${token}` } })).pipe(
      catchError((error: unknown) => {
        if (error instanceof HttpErrorResponse && error.status === 401) {
          auth.expireSession();
          return sessionEnded(router, req);
        }
        return throwError(() => error);
      }),
    );
  }),
);

function sessionEnded(router: Router, req: HttpRequest<unknown>) {
  if (!router.url.startsWith('/login')) {
    void router.navigate(['/login'], { queryParams: { reason: 'session-expired' } });
  }
  return throwError(() => new HttpErrorResponse({ status: 401, url: req.urlWithParams }));
}
```

Blast radius: one file. `auth.interceptor.spec.ts` keeps its three cases (the "no token" one stays
true for an anonymous caller) and gains four.

### 3. `core/auth.guard.ts`, `core/role.guard.ts`, `app.routes.ts`

Today ([auth.guard.ts:12-16](../../../src/frontend/buddy/src/app/core/auth.guard.ts), same shape at
[role.guard.ts:24-28](../../../src/frontend/buddy/src/app/core/role.guard.ts)):

```ts
await auth.completeLoginRedirect();

if (!auth.isAuthenticated()) {
  return router.createUrlTree(['/login']);
}
```

Proposed (both guards):

```ts
await auth.completeLoginRedirect();

// isAuthenticated() only says a token set exists; this also refreshes it, so an ended
// Keycloak session lands on /login before the page renders instead of failing its API calls.
if ((await auth.getAccessToken()) === null) {
  return router.createUrlTree(['/login'], auth.sessionExpired()
    ? { queryParams: { reason: 'session-expired' } }
    : {});
}
```

And in [app.routes.ts](../../../src/frontend/buddy/src/app/app.routes.ts), on both `guardian` and
`child`:

```ts
canActivate: [authGuard],
canActivateChild: [authGuard],
```

Blast radius: 2 guards, 2 route entries. In `auth.guard.spec.ts` and `role.guard.spec.ts`, the stub
changes from `isAuthenticated` to `getAccessToken`; every authenticated-path test in both files
builds that stub, so the change touches the shared `setup` helper rather than each test. `isAuthenticated` stays on `AuthService`; the login and
invite pages still read it.

### 4. `features/login/login.ts` / `login.html`

Read `reason` from `ActivatedRoute`'s query params into a `sessionExpired` computed signal. When
it's `'session-expired'`, show a notice above the sign-in button, styled like the existing
`redirectNote` box but in amber.

### 5. i18n

One new key in `translations/{en,da}/login.ts`:

| Key | en | da |
|---|---|---|
| `login.sessionExpired` | `Your session expired. Sign in again to continue.` | `Din session er udløbet. Log ind igen for at fortsætte.` |

Run `node .claude/skills/i18n/check-parity.mjs`.

### 6. Testing

- `auth.service.spec.ts`: `sessionExpired` turns true on a failed refresh and on a missing
  refresh token, stays false after `logout()`, and resets to false after a successful
  `completeLoginRedirect()`.
- `auth.interceptor.spec.ts`: an expired session with no token navigates to
  `/login?reason=session-expired` and errors with 401 without calling `next`; an anonymous caller
  (no token, not expired) still passes through untouched; a 401 on an authorized request calls
  `expireSession()` and navigates; a non-401 error (e.g. 500) passes through without navigating;
  no navigation when already on `/login`.
- `auth.guard.spec.ts` / `role.guard.spec.ts`: an expired session redirects with the `reason`
  query param; a never-authenticated user redirects without it.
- `login.spec.ts`: the notice shows only with `reason=session-expired`.
- e2e (new `e2e/session-expiry.spec.ts`): sign in as a disposable guardian, end their Keycloak
  sessions through the Admin API (`endKeycloakSessions` in `support/keycloak-admin-client.ts`,
  which refuses seeded users), and move the browser clock 6 minutes forward with `page.clock`.
  Then open "Calendar" from the account menu and expect `/login` with the notice. Signing in again
  through the real Keycloak form must land back on `/guardian/calendar`. Editing `sessionStorage`
  mid-page doesn't work for this: `AuthService` reads the tokens once at startup and keeps them in
  memory.

### 7. Screenshots

The `/login` route is already captured. Add a second entry in
[screenshots/pages.ts](../../../src/frontend/buddy/screenshots/pages.ts) (`name: 'login-session-expired'`,
`route: '/login'`, `path: () => '/login?reason=session-expired'`) and run `task docs:screenshots`.

## Failure and edge-case behavior

| Case | Behavior |
|---|---|
| Idle < 30 min, then navigate | Unchanged: `getAccessToken` refreshes silently |
| Idle > 30 min, then navigate | `canActivateChild` guard refresh fails -> `/login?reason=session-expired` before the page renders |
| Idle > 30 min, then an in-page action (save, toggle) | Interceptor redirects. Any unsaved form input is lost (see open questions) |
| Reload with stale tokens still stored | Guard refresh fails -> `/login` with the notice (today: error page once) |
| Anonymous share/invite page | No token and not expired -> request goes out as today |
| Several parallel calls fail together | One shared refresh (`refreshInFlight`), one navigation (skipped once on `/login`) |
| API 401 despite a token the app considered valid (key rotation, clock skew) | Treated as an expired session |
| Deliberate logout | Keycloak logout as today, no "expired" notice |
| API down (status 0 / 5xx) | Not a session problem: no redirect, pages show their load error as today |

## Explicitly out of scope for this phase

- Proactively warning before the session ends ("you'll be logged out in 2 minutes").
- Persisting tokens in `localStorage` so new tabs share a session.
- Any backend change.

## Decisions made

| Question | Decision |
|---|---|
| When does the frontend redirect? | Only when a session existed and ended (`sessionExpired`), so anonymous pages keep working |
| Send the doomed request anyway? | No: error it locally with a synthetic 401 and redirect |
| Where is the check? | Guards (`getAccessToken`, plus `canActivateChild`) before render, interceptor as the backstop for in-page actions |
| `/login` or straight to Keycloak? | `/login` with a "session expired" notice |
| Return to the interrupted page after login? | Yes, via `pending-return-url.ts`. Stored by the interceptor (`router.url`) and `authGuard` (the target `state.url`) only for an expired session. `roleRedirectGuard` takes it before the invite checks, so a winning invite still clears it, and honours it only inside the user's own role tree (whole path segments, so `/guardian-invite/...` doesn't count as `/guardian`) |
| Raise Keycloak's SSO Session Idle? | No, kept at the 30-minute default for now |
| Silent re-login? | Not possible after the idle timeout: the refresh token and SSO cookie expire together |

## Remaining open questions

- **Unsaved form input on an in-page 401.** The redirect discards it. Accepted for now.
- **Longer Keycloak sessions.** Raising SSO Session Idle (`ssoSessionIdleTimeout` in
  [buddy-realm.json](../../../.devcontainer/keycloak/buddy-realm.json) and in prod's admin console)
  was declined for now. It would make expiry rarer, especially for a child on a shared iPad, and
  needs no frontend change. `offline_access` tokens stay rejected: they're long-lived credentials
  sitting in `sessionStorage`.

## Verification

- `npx ng test --watch=false --include` for the five updated specs, then `task test:frontend`.
- `task test:e2e` including the new `session-expiry.spec.ts`.
- Manual (in-app navigation): sign in, then in the Keycloak admin console (realm `buddy` ->
  Users -> the user -> Sessions) sign the session out. Wait out the 5-minute access token, then open
  "Calendar" from the account menu. Expect `/login` with the notice; signing in returns to
  `/guardian/calendar`.
- Manual (reload): in DevTools set `expiresAt` in `sessionStorage.buddy_keycloak_tokens` to `0`,
  corrupt `refreshToken` and reload. Expect the same result.
