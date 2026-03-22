import { zipcodesService } from '../services/zipcodes.service';

export function registerZipcodesIpc(ipcMain: any): void {
  ipcMain.handle('zipcodes:list', async () => {
    return zipcodesService.getAll();
  });

  ipcMain.handle('zipcodes:cities', async () => {
    return zipcodesService.getCities();
  });

  ipcMain.handle('zipcodes:byCity', async (_e: any, city: string) => {
    return zipcodesService.getAreasByCity(city);
  });
}
