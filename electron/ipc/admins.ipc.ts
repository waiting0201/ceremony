import { adminsService } from '../services/admins.service';

export function registerAdminsIpc(ipcMain: any): void {
  ipcMain.handle('admins:list', async () => {
    return adminsService.getAll();
  });

  ipcMain.handle('admins:get', async (_e: any, id: number) => {
    return adminsService.getById(id);
  });

  ipcMain.handle('admins:create', async (_e: any, data: any) => {
    return adminsService.create(data);
  });

  ipcMain.handle('admins:update', async (_e: any, id: number, data: any) => {
    return adminsService.update(id, data);
  });

  ipcMain.handle('admins:delete', async (_e: any, id: number) => {
    return adminsService.remove(id);
  });
}
