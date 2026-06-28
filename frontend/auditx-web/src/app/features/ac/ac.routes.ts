import { Routes } from '@angular/router';

import { permissionGuard } from '../../core/guards/permission.guard';
import { Permissions } from '../../core/permissions';

/**
 * M13 — Audit Committee Workspace lazy routes (mounted at `/ac`).
 *
 * The pack surfaces require ViewACPacks; the dashboard / action items /
 * plans-awaiting surfaces are scoped to ACMember (the action-item create/close
 * and chair-acknowledge controls are further gated per-permission inside the
 * components). Restrict-visibility requires CIA.
 */
export const AC_ROUTES: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'packs' },
  {
    path: 'packs',
    title: 'AC Packs · AuditX',
    canActivate: [permissionGuard(Permissions.ViewACPacks)],
    loadComponent: () =>
      import('./pack-list/pack-list.component').then(
        (m) => m.AcPackListComponent,
      ),
  },
  {
    path: 'packs/:id',
    title: 'AC Pack · AuditX',
    canActivate: [permissionGuard(Permissions.ViewACPacks)],
    loadComponent: () =>
      import('./pack-viewer/pack-viewer.component').then(
        (m) => m.AcPackViewerComponent,
      ),
  },
  {
    path: 'dashboard',
    title: 'AC Dashboard · AuditX',
    canActivate: [permissionGuard(Permissions.ACMember)],
    loadComponent: () =>
      import('./dashboard/ac-dashboard.component').then(
        (m) => m.AcDashboardComponent,
      ),
  },
  {
    path: 'action-items',
    title: 'AC Action Items · AuditX',
    canActivate: [permissionGuard(Permissions.ACMember)],
    loadComponent: () =>
      import('./action-items/action-items.component').then(
        (m) => m.AcActionItemsComponent,
      ),
  },
  {
    path: 'plans-awaiting',
    title: 'Plans Awaiting Decision · AuditX',
    canActivate: [permissionGuard(Permissions.ACMember)],
    loadComponent: () =>
      import('./plans-awaiting/plans-awaiting.component').then(
        (m) => m.AcPlansAwaitingComponent,
      ),
  },
  {
    path: 'findings/:type/:id/restrict',
    title: 'Restrict Finding · AuditX',
    canActivate: [permissionGuard(Permissions.CIA)],
    loadComponent: () =>
      import('./restrict-visibility/restrict-visibility.component').then(
        (m) => m.AcRestrictVisibilityComponent,
      ),
  },
];