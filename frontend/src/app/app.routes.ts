import { Routes } from '@angular/router';
import { authGuard } from './core/auth/auth.guard';
import { electronReadyGuard } from './core/platform/electron-ready.guard';

export const routes: Routes = [
  // Electron-only：軟體偵測 / DB 連線設定（無 authGuard / electronReadyGuard，避免循環）
  {
    path: 'prereq',
    loadComponent: () =>
      import('./features/prereq/prereq-page').then((m) => m.PrereqPage),
    title: '系統環境檢查｜寶覺寺法會報名系統',
  },
  {
    path: 'setup',
    loadComponent: () =>
      import('./features/setup/setup-page').then((m) => m.SetupPage),
    title: '連線設定｜寶覺寺法會報名系統',
  },
  {
    path: 'login',
    loadComponent: () =>
      import('./features/login/login-page').then((m) => m.LoginPage),
    canActivate: [electronReadyGuard],
    title: '登入｜寶覺寺法會報名系統',
  },
  {
    path: '',
    loadComponent: () =>
      import('./core/layout/shell-layout/shell-layout').then((m) => m.ShellLayout),
    canActivate: [electronReadyGuard, authGuard],
    children: [
      {
        path: '',
        loadComponent: () =>
          import('./features/dashboard/dashboard-page').then((m) => m.DashboardPage),
        title: '首頁｜寶覺寺法會報名系統',
        data: { breadcrumb: '首頁', form: 'MainForm' },
      },
      {
        path: 'admins',
        loadComponent: () =>
          import('./features/admins/admins-page').then((m) => m.AdminsPage),
        title: '管理者維護',
        data: { breadcrumb: '管理者維護', form: 'AdminsForm' },
      },
      {
        path: 'believers',
        loadComponent: () =>
          import('./features/believers/believers-page').then((m) => m.BelieversPage),
        title: '信眾維護',
        data: { breadcrumb: '信眾維護', form: 'BelieverForm' },
      },
      {
        path: 'signups',
        loadComponent: () =>
          import('./features/signups/signup-list-page').then((m) => m.SignupListPage),
        title: '報名維護',
        data: { breadcrumb: '報名維護', form: 'SignupForm' },
      },
      {
        path: 'signups/new',
        loadComponent: () =>
          import('./features/signups/signup-edit-page').then((m) => m.SignupEditPage),
        title: '新增報名',
        data: { breadcrumb: '新增報名', form: 'NewSignupForm' },
      },
      {
        path: 'signups/:id/edit',
        loadComponent: () =>
          import('./features/signups/signup-edit-page').then((m) => m.SignupEditPage),
        title: '修改報名',
        data: { breadcrumb: '修改報名', form: 'EditSignupForm' },
      },
      {
        path: 'signups/:id/logs',
        loadComponent: () =>
          import('./features/signups/signup-logs-page').then((m) => m.SignupLogsPage),
        title: '報名變更紀錄',
        data: { breadcrumb: '變更紀錄', form: 'SignupLogForm' },
      },
      {
        path: 'prepay',
        loadComponent: () =>
          import('./features/prepay/prepay-page').then((m) => m.PrepayPage),
        title: '載入預繳',
        data: { breadcrumb: '載入預繳', form: 'LoadPrepayForm' },
      },
      {
        path: 'backup',
        loadComponent: () =>
          import('./features/backup/backup-page').then((m) => m.BackupPage),
        title: '資料備份',
        data: { breadcrumb: '資料備份', form: 'MainForm' },
      },
      {
        path: 'categories',
        loadComponent: () =>
          import('./features/categories/categories-page').then((m) => m.CategoriesPage),
        title: '法會類型維護',
        data: { breadcrumb: '法會類型', form: 'CeremonyCategoryForm' },
      },
      {
        path: 'reports/preview',
        loadComponent: () =>
          import('./features/reports/reports-preview-page').then((m) => m.ReportsPreviewPage),
        title: '列印預覽',
        data: { breadcrumb: '列印預覽', form: '(列印預覽)' },
      },
      {
        path: 'reports/preview/:type',
        loadComponent: () =>
          import('./features/reports/reports-preview-page').then((m) => m.ReportsPreviewPage),
        title: '列印預覽',
        data: { breadcrumb: '列印預覽', form: '(列印預覽)' },
      },
    ],
  },
  { path: '**', redirectTo: '' },
];
