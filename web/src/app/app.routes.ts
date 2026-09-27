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
      {
        path: 'account/password',
        loadComponent: () =>
          import('./features/account/change-password/change-password').then((m) => m.ChangePassword),
      },
    ],
  },
  { path: '**', redirectTo: '' },
];
