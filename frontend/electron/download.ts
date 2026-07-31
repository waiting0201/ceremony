// 備份檔下載另存：原生「另存新檔」對話框 → 以 Electron net 串流 GET 後端 download endpoint 寫到本機。
// 為何不在 renderer 抓 blob：.bak 動輒 ~100MB+，由 main 串流寫檔避免 renderer 記憶體爆掉。
// 註：「立即備份」本身直接寫到伺服器 Backup:Directory（同機部署 = D:\Backup），不經此流程；
// 此 downloadBackup 保留供需要時把既有 .bak 另存到他處（目前 UI 未掛，屬備用能力）。
import { dialog, BrowserWindow } from 'electron';
import { streamApiToFile } from './api-stream';

export interface DownloadResult {
  ok: boolean;
  canceled?: boolean;
  path?: string;
  error?: string;
}

export async function downloadBackup(
  win: BrowserWindow,
  apiBase: string,
  fileName: string,
  token: string,
): Promise<DownloadResult> {
  const { canceled, filePath } = await dialog.showSaveDialog(win, {
    title: '另存備份檔',
    defaultPath: fileName,
    filters: [
      { name: '資料庫備份', extensions: ['bak', 'trn'] },
      { name: '所有檔案', extensions: ['*'] },
    ],
  });
  if (canceled || !filePath) return { ok: false, canceled: true };

  const r = await streamApiToFile(
    apiBase,
    `/backup/${encodeURIComponent(fileName)}/download`,
    token,
    filePath,
  );
  return r.ok ? { ok: true, path: filePath } : { ok: false, error: r.error };
}
