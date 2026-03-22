import { believersService } from '../services/believers.service';

export function registerBelieversIpc(ipcMain: any): void {
  ipcMain.handle('believers:search', async (_e: any, params: any) => {
    return believersService.search(params);
  });

  ipcMain.handle('believers:get', async (_e: any, id: string) => {
    return believersService.getById(id);
  });

  ipcMain.handle('believers:create', async (_e: any, data: any) => {
    return believersService.create(data);
  });

  ipcMain.handle('believers:update', async (_e: any, id: string, data: any) => {
    return believersService.update(id, data);
  });

  ipcMain.handle('believers:delete', async (_e: any, id: string) => {
    return believersService.remove(id);
  });

  ipcMain.handle('believers:lookup', async (_e: any, keyword: string) => {
    return believersService.lookup(keyword);
  });
}
