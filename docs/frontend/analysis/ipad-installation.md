# Installing Buddy on a kid's iPad

Buddy's frontend is a plain Angular SPA today — no manifest, no service worker, no native
wrapper (see [Frontend overview](../README.md)). This document weighs the options for getting it
onto a child's iPad/iPadOS as an installed app, including whether push notifications are possible
that way, and what each option costs. Status: analysis only — nothing here is implemented yet.

## The three options

All three end with an icon on the home screen. They differ in what it takes to get there and
what you're signed up for afterward.

| Option | Cost | Build effort | Updates | Push notifications | Feel |
|---|---|---|---|---|---|
| Installed web app (PWA) | $0 | 1–2 days | Instant, on refresh | Yes — iOS 16.4+ | Standalone, no browser chrome |
| Capacitor-wrapped native | $99/yr | 3–5 days | Re-upload build (TestFlight) | Yes — full APNs | True native app |
| Full native rewrite | $99/yr | Weeks–months | Re-upload build (TestFlight) | Yes — full APNs | True native app |

### 1. Installed web app (PWA) — recommended

**Pros**

- Zero incremental cost — runs on the Caddy/Docker setup Buddy already deploys with
  ([deploy/README.md](../../../deploy/README.md))
- Ship changes by pushing to production — no review queue, no waiting
- Full-screen, standalone icon on the home screen once added — no address bar
- Real push notifications since iOS 16.4 (March 2023), once it's on the home screen, via the
  standard Web Push API and Angular's `SwPush`
- One codebase — the same Angular app in
  [src/frontend/buddy](../../../src/frontend/buddy), nothing new to maintain

**Cons**

- Installed by hand: Safari → Share → "Add to Home Screen" — can't be pushed remotely to a device
- If the icon gets deleted, or iOS reclaims storage under pressure, it needs re-adding
- Notifications are plainer than native — no rich media, fewer interaction options
- Nothing to point anyone toward in the App Store

Needs: a manifest, a service worker (`ng add @angular/pwa` as a starting point), icons, and
`SwPush` wiring for push. Effort: roughly 1–2 days.

### 2. Native shell around the same app (Capacitor) — fallback

**Pros**

- Reuses the existing Angular code almost unchanged — Capacitor wraps it in a native shell
- Real app icon, real APNs push, reliable delivery in the background
- Installs privately through TestFlight — never has to touch the public App Store
- Works cleanly with Guided Access and Screen Time, same as any other native app

**Cons**

- Needs an Apple Developer Program membership — $99/year, or the app stops opening
- Needs a Mac with Xcode to build and sign; the current devcontainer is Linux-based
  ([.devcontainer/devcontainer.json](../../../.devcontainer/devcontainer.json)), so this is new
  tooling
- TestFlight builds expire after 90 days — someone has to re-upload periodically
- A second build pipeline to keep alive alongside the web one

Cost: $99/year plus a Mac for builds. Effort: roughly 3–5 days once the Mac and developer account
exist.

### 3. Full native rewrite (SwiftUI) — not worth it here

The only advantage over Capacitor is no web-technology ceiling — full access to every iOS API,
best possible polish and performance. Against that: a second, parallel codebase in a different
language forever, the same $99/year fee and Mac/Xcode requirement as Capacitor without reusing
any existing code, and weeks to months of engineering time for a feature set Buddy already has in
Angular. This only makes sense if Buddy grows into a large, long-lived, multi-platform product
where native polish is the entire point — not for a family tool.

## Pricing at a glance

The web app route uses infrastructure Buddy already runs. Everything native routes through the
same Apple fee, no matter how the native app is built.

| Item | Needed for | Cost |
|---|---|---|
| Hosting Buddy's web build | Installed web app | $0 — existing Caddy/Docker deploy |
| Apple Developer Program (Individual) | TestFlight and/or the App Store | $99/year |
| Free "Personal Team" Xcode signing | Testing a native build without paying | $0, but the app stops opening after 7 days unless reinstalled from a Mac |
| TestFlight distribution | Private install to your own family's iPad | Included in the $99/year membership |
| Public App Store listing | Only if distributing beyond your own family | Included in membership, plus Apple's review and, for a public "Kids" listing, COPPA/Kids Category compliance |
| Capacitor tooling | Wrapping the existing Angular app | $0 — open source |
| A Mac with Xcode | Building or signing anything for iOS | $0 if you already have one; otherwise the price of a Mac |

## Locking it down for a kid

These controls live at the iPad/iOS level, not the app level — they apply almost identically
whether Buddy ends up installed as a PWA or as a native app.

- **Guided Access** (Settings → Accessibility → Guided Access, then triple-click the side
  button) pins the iPad to a single app — works the same for an installed PWA as for a native
  app.
- **Screen Time / Content & Privacy Restrictions** control which apps show up and for how long.
  An installed PWA appears as an ordinary icon and can be limited or always-allowed exactly like
  any native app.
- **Family Sharing with a Child Apple ID** gives remote Screen Time management and Ask to Buy
  from your own device. Worth setting up regardless of which install path is chosen.
- **Kids Category / COPPA** only applies if Buddy is ever listed publicly on the App Store under
  "Kids" for other families' children. A private install for your own child — PWA or TestFlight —
  doesn't trigger it.
- **MDM / Apple Configurator** is only relevant when managing a fleet of devices (a classroom, a
  clinic). Overkill for a single family iPad.

## Recommendation

Ship the installed web app first. It reuses everything that already exists in
[src/frontend/buddy](../../../src/frontend/buddy), costs nothing beyond the hosting Buddy already
has, and gets push notifications working on any iOS 16.4+ iPad the moment it's added to the home
screen. Guided Access and Screen Time lock it down exactly as well as a native app would, so the
kid-safety side of this doesn't depend on which route gets picked.

Revisit this if push notifications turn out to be unreliable in practice, or the "tap Share → Add
to Home Screen" install step becomes a real obstacle for whoever's setting up the iPad. At that
point, a Capacitor wrapper distributed through TestFlight ($99/year) is the next step up — not a
full native rewrite.

Apple's developer fees, TestFlight limits, and Kids Category rules are current as of August
2026 — worth a quick check at developer.apple.com before paying, since Apple revises these terms
periodically.
