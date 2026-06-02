// 備份檔下載另存：原生「另存新檔」對話框 → 以 Electron net 串流 GET 後端 download endpoint 寫到本機。
// 為何不在 renderer 抓 blob：.bak 動輒 ~100MB+，由 main 串流寫檔避免 renderer 記憶體爆掉。
import { dialog, net, BrowserWindow } from 'electron';
import fs from 'fs';

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

  return new Promise<DownloadResult>((resolve) => {
    const url = `${apiBase}/backup/${encodeURIComponent(fileName)}/download`;
    const request = net.request({ method: 'GET', url });
    request.setHeader('Authorization', `Bearer ${token}`);

    request.on('response', (response) => {
      const status = response.statusCode ?? 0;
      if (status !== 200) {
        let body = '';
        response.on('data', (c) => (body += c.toString()));
        response.on('end', () =>
          resolve({ ok: false, error: `下載失敗（HTTP ${status}）${body}`.trim() }),
        );
        return;
      }
      const out = fs.createWriteStream(filePath);
      out.on('error', (e) => resolve({ ok: false, error: e.message }));
      response.on('data', (chunk) => out.write(chunk));
      response.on('end', () => out.end(() => resolve({ ok: true, path: filePath })));
      response.on('error', (e: Error) => resolve({ ok: false, error: e.message }));
    });

    request.on('error', (e) => resolve({ ok: false, error: e.message }));
    request.end();
  });
}
