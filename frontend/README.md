# AuditX Enterprise — Frontend

Angular 21 single-page application for the AuditX Enterprise on-premises bank
internal-audit platform. The workspace lives in [`auditx-web/`](./auditx-web).

- **Framework:** Angular 21 (standalone components, signals, zoneless change detection, lazy-loaded routes, typed reactive forms)
- **UI:** Angular Material (Material 3) + SCSS theming, responsive & accessible
- **State:** Angular signals (no NgRx)
- **PWA:** `@angular/service-worker` (enabled in production builds)
- **Tests:** Jasmine + Karma (ChromeHeadless)

## Prerequisites

- Node 24, npm 11
- Angular CLI 21 (`npm i -g @angular/cli`) — optional; local `ng` works via `npm run`
- Google Chrome (for `ng test`)

## Install

```bash
cd auditx-web
npm install
```

## Run (dev server with backend proxy)

The dev server proxies `/api` to the backend at `http://localhost:8080`
(see `auditx-web/proxy.conf.json`). The proxy is wired into the `serve` target,
so a plain serve is enough:

```bash
cd auditx-web
npm start
# or: ng serve
```

App: http://localhost:4200 — API calls to `/api/v1/*` are forwarded to
`http://localhost:8080/api/v1/*` with cookies (`withCredentials`) preserved.

## Build

```bash
cd auditx-web
ng build                                 # production (default), emits dist/auditx-web + service worker
ng build --configuration development     # dev build (uses environment.development.ts)
```

Production build output: `auditx-web/dist/auditx-web`.

## Test

```bash
cd auditx-web
ng test --watch=false --browsers=ChromeHeadless     # one-shot headless run
npm run test:ci                                      # same, with CI Chrome flags (--no-sandbox)
```

> If Chrome is not installed in your environment the runner will report
> "no binary for ChromeHeadless". The Karma config (`karma.conf.js`) is correct;
> set `CHROME_BIN` to a Chrome/Chromium executable to run elsewhere. A
> `ChromeHeadlessCI` launcher (sandbox-disabled) is provided for CI.

## Environments

- `src/environments/environment.ts` — production (`apiBaseUrl: '/api/v1'`)
- `src/environments/environment.development.ts` — dev (`apiBaseUrl: '/api/v1'`)

Both target the same relative base path; the dev server proxy handles the
cross-origin forward to `:8080`. In production the SPA is served from the same
origin as the API.

## Notes / decisions

- **Auth:** the session JWT is an HttpOnly Secure cookie the SPA cannot read.
  All HTTP traffic sends `withCredentials: true` (guaranteed by an interceptor).
  Session state is derived from `GET /auth/session` and `POST /auth/login` and
  held in signals on `AuthService`.
- **Zoneless:** the app uses Angular's zoneless change detection. Specs provide
  `provideZonelessChangeDetection()`.
- **Maker-checker:** role create/update handle both the direct (200/201 RoleDto)
  and gated (202 `{ pendingActionId }`) responses, surfacing an info toast on 202.
- **Errors:** an HTTP interceptor surfaces RFC 7807 `title`/`detail`/`field_errors`
  as Material snackbars and redirects to `/login` on 401.
