import { Routes } from '@angular/router';
import { homeRedirectGuard, roleGuard } from './core/role.guard';

export const routes: Routes = [
  { path: '', pathMatch: 'full', canActivate: [homeRedirectGuard], children: [] },
  {
    path: 'login',
    canActivate: [homeRedirectGuard],
    loadComponent: () => import('./features/auth/login/login').then((m) => m.Login),
  },
  {
    path: 'office',
    canActivate: [roleGuard('Admin', 'Dispatcher')],
    loadComponent: () => import('./layout/office-shell/office-shell').then((m) => m.OfficeShell),
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
      { path: 'dashboard', loadComponent: () => import('./features/dashboard/dashboard').then((m) => m.Dashboard) },
      {
        path: 'customers',
        loadComponent: () => import('./features/customers/customer-list').then((m) => m.CustomerList),
      },
      {
        path: 'customers/:id',
        loadComponent: () => import('./features/customers/customer-detail').then((m) => m.CustomerDetail),
      },
      {
        path: 'work-orders',
        loadComponent: () => import('./features/work-orders/work-order-list').then((m) => m.WorkOrderList),
      },
      {
        path: 'work-orders/:id',
        loadComponent: () => import('./features/work-orders/work-order-detail').then((m) => m.WorkOrderDetail),
      },
      {
        path: 'technicians',
        loadComponent: () => import('./features/technicians/technician-list').then((m) => m.TechnicianList),
      },
      {
        path: 'time-off',
        loadComponent: () => import('./features/time-off/time-off-approvals').then((m) => m.TimeOffApprovals),
      },
      {
        path: 'settings/skills',
        canActivate: [roleGuard('Admin')],
        loadComponent: () => import('./features/technicians/skills').then((m) => m.Skills),
      },
      {
        path: 'settings/users',
        canActivate: [roleGuard('Admin')],
        loadComponent: () => import('./features/settings/users/users').then((m) => m.Users),
      },
      {
        path: 'settings/company',
        canActivate: [roleGuard('Admin')],
        loadComponent: () =>
          import('./features/settings/company-settings/company-settings').then((m) => m.CompanySettingsPage),
      },
      {
        path: 'account/password',
        loadComponent: () =>
          import('./features/account/change-password/change-password').then((m) => m.ChangePassword),
      },
    ],
  },
  {
    path: 'tech',
    canActivate: [roleGuard('Technician')],
    loadComponent: () => import('./layout/tech-shell/tech-shell').then((m) => m.TechShell),
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'my-jobs' },
      { path: 'my-jobs', loadComponent: () => import('./features/tech/my-jobs/my-jobs').then((m) => m.MyJobs) },
      { path: 'time-off', loadComponent: () => import('./features/time-off/my-time-off').then((m) => m.MyTimeOff) },
      {
        path: 'account/password',
        loadComponent: () =>
          import('./features/account/change-password/change-password').then((m) => m.ChangePassword),
      },
    ],
  },
  { path: '**', redirectTo: '' },
];
