import { ceremonyCategorysService } from '../services/ceremony-categories.service';

export function registerCeremonyCategoriesIpc(ipcMain: any): void {
  ipcMain.handle('ceremony-categories:tree', async () => {
    return ceremonyCategorysService.getTree();
  });

  ipcMain.handle('ceremony-categories:list', async () => {
    return ceremonyCategorysService.getAll();
  });

  ipcMain.handle('ceremony-categories:create', async (_e: any, data: any) => {
    return ceremonyCategorysService.create(data);
  });

  ipcMain.handle('ceremony-categories:update', async (_e: any, id: string, data: any) => {
    return ceremonyCategorysService.update(id, data);
  });

  ipcMain.handle('ceremony-categories:delete', async (_e: any, id: string) => {
    return ceremonyCategorysService.remove(id);
  });

  ipcMain.handle('ceremony-categories:nextSort', async (_e: any, parentId: string | null) => {
    return ceremonyCategorysService.getNextSort(parentId);
  });
}
