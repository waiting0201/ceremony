import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { ceremony, isElectron } from './electron';

// 套在 /login 與主程式（''）上：Electron 模式下若 prereq 未滿足 → /prereq；
// 若尚未連線（無 apiBase）→ /setup。瀏覽器模式直接放行（維持純 SPA 行為）。
// 注意：inject(Router) 必須在第一個 await 之前呼叫（await 後會失去 injection context）。
export const electronReadyGuard: CanActivateFn = async () => {
  if (!isElectron()) return true;
  const bridge = ceremony();
  if (!bridge) return true;

  const router = inject(Router);
  const status = await bridge.getStatus();

  if (!status.prereqsOk) return router.parseUrl('/prereq');
  if (!status.connected) return router.parseUrl('/setup');
  return true;
};
