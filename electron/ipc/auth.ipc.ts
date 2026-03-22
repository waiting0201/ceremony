import { login, logout, getSession } from '../services/auth.service';

export function registerAuthIpc(ipcMain: any): void {
  ipcMain.handle('auth:login', async (_event: any, username: string, password: string) => {
    return login(username, password);
  });

  ipcMain.handle('auth:logout', () => {
    return logout();
  });

  ipcMain.handle('auth:session', () => {
    return getSession();
  });
}
