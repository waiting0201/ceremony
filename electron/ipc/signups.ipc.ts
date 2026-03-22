import { signupsService } from '../services/signups.service';
import { signupLogsService } from '../services/signup-logs.service';

export function registerSignupsIpc(ipcMain: any): void {
  ipcMain.handle('signups:search', async (_e: any, params: any) => {
    return signupsService.search(params);
  });

  ipcMain.handle('signups:get', async (_e: any, id: string) => {
    return signupsService.getById(id);
  });

  ipcMain.handle('signups:create', async (_e: any, data: any) => {
    return signupsService.create(data);
  });

  ipcMain.handle('signups:update', async (_e: any, id: string, data: any) => {
    return signupsService.update(id, data);
  });

  ipcMain.handle('signups:delete', async (_e: any, id: string) => {
    return signupsService.remove(id);
  });

  ipcMain.handle('signups:nextNumber', async (_e: any, year: number, ccId: string, stype: number) => {
    return signupsService.getNextNumber(year, ccId, stype);
  });

  // Signup Logs
  ipcMain.handle('signup-logs:search', async (_e: any, params: any) => {
    return signupLogsService.search(params);
  });

  ipcMain.handle('signup-logs:create', async (_e: any, data: any) => {
    return signupLogsService.create(data);
  });
}
