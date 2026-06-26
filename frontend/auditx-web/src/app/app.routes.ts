import { Routes } from '@angular/router';

import { authGuard } from './core/guards/auth.guard';
import { permissionGuard } from './core/guards/permission.guard';
import { Permissions } from './core/permissions';

export const routes: Routes = [
  {
    path: 'login',
    title: 'Sign in · AuditX',
    loadComponent: () =>
      import('./features/auth/login/login.component').then(
        (m) => m.LoginComponent,
      ),
  },
  {
    path: 'awaiting-role',
    title: 'Access pending · AuditX',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/auth/awaiting-role/awaiting-role.component').then(
        (m) => m.AwaitingRoleComponent,
      ),
  },
  {
    path: '',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./shared/layout/main-layout.component').then(
        (m) => m.MainLayoutComponent,
      ),
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
      {
        path: 'dashboard',
        title: 'Dashboard · AuditX',
        loadComponent: () =>
          import('./features/dashboard/dashboard.component').then(
            (m) => m.DashboardComponent,
          ),
      },
      {
        path: 'admin/users',
        title: 'Users · AuditX',
        canActivate: [permissionGuard(Permissions.ManageUsers)],
        loadComponent: () =>
          import(
            './features/admin/identity/users-list/users-list.component'
          ).then((m) => m.UsersListComponent),
      },
      {
        path: 'admin/users/:id',
        title: 'User detail · AuditX',
        canActivate: [permissionGuard(Permissions.ManageUsers)],
        loadComponent: () =>
          import(
            './features/admin/identity/user-detail/user-detail.component'
          ).then((m) => m.UserDetailComponent),
      },
      {
        path: 'admin/roles',
        title: 'Roles · AuditX',
        canActivate: [permissionGuard(Permissions.ManageRoles)],
        loadComponent: () =>
          import(
            './features/admin/identity/roles-list/roles-list.component'
          ).then((m) => m.RolesListComponent),
      },
      {
        path: 'admin/roles/new',
        title: 'New role · AuditX',
        canActivate: [permissionGuard(Permissions.ManageRoles)],
        loadComponent: () =>
          import(
            './features/admin/identity/role-editor/role-editor.component'
          ).then((m) => m.RoleEditorComponent),
      },
      {
        path: 'admin/roles/:id',
        title: 'Edit role · AuditX',
        canActivate: [permissionGuard(Permissions.ManageRoles)],
        loadComponent: () =>
          import(
            './features/admin/identity/role-editor/role-editor.component'
          ).then((m) => m.RoleEditorComponent),
      },
      {
        path: 'admin/maker-checker',
        title: 'Maker-Checker · AuditX',
        canActivate: [
          permissionGuard(
            Permissions.ManageRoles,
            Permissions.ManageUsers,
            Permissions.ApproveMakerChecker,
          ),
        ],
        loadComponent: () =>
          import(
            './features/admin/identity/maker-checker-queue/maker-checker-queue.component'
          ).then((m) => m.MakerCheckerQueueComponent),
      },
    ],
  },
  {
    path: '**',
    title: 'Not found · AuditX',
    loadComponent: () =>
      import('./features/not-found/not-found.component').then(
        (m) => m.NotFoundComponent,
      ),
  },
];
