# auditx-web

Angular 21 workspace for the **AuditX Enterprise** internal-audit SPA.

See the parent [`../README.md`](../README.md) for full install / run / build /
test instructions and architectural notes.

## Quick reference

```bash
npm install                                          # install deps
npm start                                            # dev server (proxy -> http://localhost:8080)
ng build                                             # production build -> dist/auditx-web
ng test --watch=false --browsers=ChromeHeadless      # unit tests (Jasmine/Karma)
```

## Layout

```
src/app/
  core/         ApiService, AuthService, domain services, interceptors, guards, models
  shared/       layout shell, reusable UI components, pipes
  features/
    auth/       login, awaiting-role
    dashboard/  landing dashboard
    admin/identity/
                users-list, user-detail, roles-list, role-editor,
                maker-checker-queue, dialogs
    not-found/  404
```
