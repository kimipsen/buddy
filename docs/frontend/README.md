# Frontend documentation

This folder documents the Angular frontend for Buddy.

## Purpose

The frontend is a role-aware single-page app that supports two main user flows:

- guardian flows for creating and managing child accounts, shared calendars, and household coordination
- child flows for seeing a personalized day view and linked guardians

The app authenticates with Keycloak and then calls the Buddy API with the issued access token.

## App shell

The app is bootstrapped in [src/frontend/buddy/src/app/app.config.ts](../../src/frontend/buddy/src/app/app.config.ts). It wires together:

- router configuration
- HTTP client with auth interceptor
- runtime config loader
- browser app initialization

The top-level route setup is in [src/frontend/buddy/src/app/app.routes.ts](../../src/frontend/buddy/src/app/app.routes.ts). It divides the app into:

- `/login` for unauthenticated users
- `/guardian` for guardian dashboard screens
- `/child` for child-focused screens
- root redirect logic based on resolved role

## Authentication flow

Authentication is centered on the services in [src/frontend/buddy/src/app/core](../../src/frontend/buddy/src/app/core):

- `AuthService` handles the Keycloak authorization code exchange, token refresh, and logout
- `RuntimeConfigService` loads config from `/config/runtime-config.json`
- `authGuard` blocks unauthenticated access
- `roleRedirectGuard` sends users to the correct route after the account role is resolved

The actual role is derived from guardian relationships, not from a stored role flag. See [src/frontend/buddy/src/app/core/account.service.ts](../../src/frontend/buddy/src/app/core/account.service.ts).

## Feature layout

The Angular app currently has two main feature areas:

### Guardian feature

The guardian dashboard is implemented in [src/frontend/buddy/src/app/features/guardian](../../src/frontend/buddy/src/app/features/guardian).

Current responsibilities:

- display a dashboard shell
- list linked children
- create child accounts and capture the one-time temporary password
- sign out of the current session

The route definition is in [src/frontend/buddy/src/app/features/guardian/guardian.routes.ts](../../src/frontend/buddy/src/app/features/guardian/guardian.routes.ts).

### Child feature

The child-facing area is implemented in [src/frontend/buddy/src/app/features/child](../../src/frontend/buddy/src/app/features/child).

Current responsibilities:

- display the child home screen
- show guardian links when available
- provide the child-specific entry route

The route definition is in [src/frontend/buddy/src/app/features/child/child.routes.ts](../../src/frontend/buddy/src/app/features/child/child.routes.ts).

### Login feature

The login screen is in [src/frontend/buddy/src/app/features/login](../../src/frontend/buddy/src/app/features/login). It starts the Keycloak redirect flow and keeps the sign-in UX cleanly separated from the rest of the app.

## Shared services

The shared domain services live under [src/frontend/buddy/src/app/core](../../src/frontend/buddy/src/app/core):

- `AccountService` resolves whether the user is a guardian or child
- `GuardiansService` calls the backend guardian endpoints
- `AuthInterceptor` attaches the access token to outgoing requests
- `UserEventsService` is available for future user event stream integration

## Current status

The frontend is in an early product-shell stage. The screens are built as functional route shells rather than a complete scheduling UI. The core pieces already in place are:

- Keycloak authentication and token lifecycle
- route guards and role-based redirecting
- guardian-child provisioning flow
- child/guardian role separation
- placeholder guardian and child dashboard layouts

The next major frontend work should be the concrete scheduling views for calendars, recurring tasks, and child medication dose tracking.

## Local development

From the frontend app folder:

```bash
cd src/frontend/buddy
npm install
npm start
```

The app runs by default on the Angular dev server at http://localhost:4200/.
