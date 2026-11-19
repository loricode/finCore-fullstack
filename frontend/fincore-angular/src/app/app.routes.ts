import { Routes } from '@angular/router';
import { Permissions } from './core/auth/auth.models';
import { authGuard, guestGuard, permissionGuard } from './core/auth/auth.guards';

export const routes: Routes = [
  {
    path: 'login',
    canActivate: [guestGuard],
    title: 'Iniciar sesión · FinCore',
    loadComponent: () => import('./features/login/login.page').then((m) => m.LoginPage),
  },
  {
    path: '',
    canActivate: [authGuard],
    loadComponent: () => import('./features/shell/shell.component').then((m) => m.ShellComponent),
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'inicio' },
      {
        path: 'inicio',
        title: 'Inicio · FinCore',
        loadComponent: () => import('./features/home/home.page').then((m) => m.HomePage),
      },
      {
        path: 'operaciones',
        title: 'Operaciones · FinCore',
        canActivate: [permissionGuard(Permissions.operationsRead)],
        loadComponent: () => import('./features/operations/operations.page').then((m) => m.OperationsPage),
      },
      {
        path: 'conciliacion',
        title: 'Conciliación · FinCore',
        canActivate: [permissionGuard(Permissions.reconciliationRead)],
        loadComponent: () => import('./features/reconciliation/reconciliation.page').then((m) => m.ReconciliationPage),
      },
      {
        path: 'reportes',
        title: 'Reportes · FinCore',
        canActivate: [permissionGuard(Permissions.reportsRead)],
        loadComponent: () => import('./features/reports/reports.page').then((m) => m.ReportsPage),
      },
      {
        path: 'sucursales',
        title: 'Sucursales · FinCore',
        loadComponent: () => import('./features/branches/branches.page').then((m) => m.BranchesPage),
      },
      {
        path: 'usuarios',
        title: 'Usuarios · FinCore',
        canActivate: [permissionGuard(Permissions.usersManage)],
        loadComponent: () => import('./features/users/users.page').then((m) => m.UsersPage),
      },
      {
        path: 'sin-permiso',
        title: 'Sin permiso · FinCore',
        loadComponent: () => import('./features/forbidden/forbidden.page').then((m) => m.ForbiddenPage),
      },
    ],
  },
  { path: '**', redirectTo: '' },
];
