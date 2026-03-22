import type { Routes } from '@angular/router';
import { authGuard } from './core/guards/auth.guard';
import { AppLayoutComponent } from './layout/app-layout.component';

export const routes: Routes = [
  {
    path: 'login',
    loadChildren: () => import('./features/login/login.routes').then((m) => m.loginRoutes),
  },
  {
    path: '',
    component: AppLayoutComponent,
    canActivate: [authGuard],
    children: [
      {
        path: 'dashboard',
        loadChildren: () =>
          import('./features/dashboard/dashboard.routes').then((m) => m.dashboardRoutes),
      },
      {
        path: 'admins',
        loadChildren: () =>
          import('./features/admins/admins.routes').then((m) => m.adminsRoutes),
      },
      {
        path: 'ceremony-categories',
        loadChildren: () =>
          import('./features/ceremony-categories/ceremony-categories.routes').then(
            (m) => m.ceremonyCategoriesRoutes,
          ),
      },
      {
        path: 'believers',
        loadChildren: () =>
          import('./features/believers/believers.routes').then((m) => m.believersRoutes),
      },
      {
        path: 'signups',
        loadChildren: () =>
          import('./features/signups/signups.routes').then((m) => m.signupsRoutes),
      },
      {
        path: 'signup-logs',
        loadChildren: () =>
          import('./features/signup-logs/signup-logs.routes').then((m) => m.signupLogsRoutes),
      },
      {
        path: 'reports',
        loadChildren: () =>
          import('./features/reports/reports.routes').then((m) => m.reportsRoutes),
      },
      { path: '', redirectTo: 'dashboard', pathMatch: 'full' },
    ],
  },
];
