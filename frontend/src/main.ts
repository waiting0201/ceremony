import { bootstrapApplication } from '@angular/platform-browser';
import { appConfig } from './app/app.config';
import { App } from './app/app';
import { environment } from './environments/environment';

// Electron sidecar 模式：main process 用動態 port 啟動 API 後，透過 query string ?apiBase=...
// 把 API base 傳進來。在 bootstrap 前覆寫 environment.apiBaseUrl（各 *.api.ts 在 DI 建構時才讀取，
// 此時尚未建構，故覆寫有效）。瀏覽器 / ng serve 無此參數 → 維持 environment.ts 預設值。
const apiBase = new URLSearchParams(window.location.search).get('apiBase');
if (apiBase) {
  (environment as { apiBaseUrl: string }).apiBaseUrl = apiBase;
}

bootstrapApplication(App, appConfig).catch((err) => console.error(err));
