import { registerAuthIpc } from './auth.ipc';
import { registerAdminsIpc } from './admins.ipc';
import { registerZipcodesIpc } from './zipcodes.ipc';
import { registerCeremonyCategoriesIpc } from './ceremony-categories.ipc';
import { registerBelieversIpc } from './believers.ipc';
import { registerSignupsIpc } from './signups.ipc';
import { registerReportsIpc } from './reports.ipc';
import { registerBackupIpc } from './backup.ipc';

export function registerAllIpc(ipcMain: any, getMainWindow?: () => any): void {
  const getWin = getMainWindow || (() => null);
  registerAuthIpc(ipcMain);
  registerAdminsIpc(ipcMain);
  registerZipcodesIpc(ipcMain);
  registerCeremonyCategoriesIpc(ipcMain);
  registerBelieversIpc(ipcMain);
  registerSignupsIpc(ipcMain);
  registerReportsIpc(ipcMain, getWin);
  registerBackupIpc(ipcMain, getWin);
}
