import { backupDatabase } from '../services/backup.service';

export function registerBackupIpc(ipcMain: any, getMainWindow: () => any): void {
  const { dialog } = require('electron');

  ipcMain.handle('backup:run', async () => {
    try {
      const win = getMainWindow();
      const { filePath } = await dialog.showSaveDialog(win, {
        title: '選擇備份路徑',
        defaultPath: `Ceremony_${new Date().toISOString().slice(0, 10)}.bak`,
        filters: [{ name: 'SQL Server Backup', extensions: ['bak'] }],
      });
      if (!filePath) return { success: false, message: '已取消' };
      return backupDatabase(filePath);
    } catch (e: any) {
      return { success: false, message: e.message };
    }
  });
}
