// Electron 主行程 → sidecar API 的串流下載（GET → 寫檔）。
//
// 為何由 main 抓而不是 renderer 抓完再 IPC 傳 buffer：批次列印 PDF 可達數百 MB，
// structured clone 會在 main 再複製一份，renderer + main 雙份記憶體 = OOM 風險。
// 原本只有 download.ts（.bak 另存）用這條路，列印通道改走同一套 → 抽成共用函式。
//
// net.request 是 Electron 主行程的 HTTP client，不是瀏覽器 fetch → 不受 CORS 限制，
// 一定讀得到 X-Report-Page-Size / Content-Disposition（renderer 那條路才需要 WithExposedHeaders）。
import { net } from 'electron';
import fs from 'fs';
import path from 'path';

export interface StreamResult {
  ok: boolean;
  /** 小寫 header 名 → 值（多值取第一個）。失敗時可能為空。 */
  headers: Record<string, string>;
  error?: string;
}

/**
 * GET `${apiBase}${apiPath}` 並串流寫到 destPath。
 * 非 200 時讀完 body 嘗試解析後端的 { errorCode, message }，把 message 原樣往上帶，
 * 讓 renderer 顯示的錯誤訊息與走 HttpClient 的路徑一致（而不是英文 HTTP status）。
 *
 * 目錄由本函式負責補齊：createWriteStream 不會自己建父目錄，而列印通道的 destPath 是
 * os.tmpdir()/ceremony-print/…（本行程自己造的目錄）→ 乾淨機器上第一次列印必 ENOENT。
 * 這是呼叫端不該重複記住的前置條件，所以收在這裡。
 */
export async function streamApiToFile(
  apiBase: string,
  apiPath: string,
  token: string,
  destPath: string,
): Promise<StreamResult> {
  try {
    await fs.promises.mkdir(path.dirname(destPath), { recursive: true });
  } catch (e) {
    return { ok: false, headers: {}, error: (e as Error).message };
  }

  return new Promise<StreamResult>((resolve) => {
    const request = net.request({ method: 'GET', url: `${apiBase}${apiPath}` });
    request.setHeader('Authorization', `Bearer ${token}`);

    request.on('response', (response) => {
      const status = response.statusCode ?? 0;
      const headers: Record<string, string> = {};
      for (const [k, v] of Object.entries(response.headers)) {
        headers[k.toLowerCase()] = Array.isArray(v) ? v[0] : String(v);
      }

      if (status !== 200) {
        let body = '';
        response.on('data', (c) => (body += c.toString()));
        response.on('end', () =>
          resolve({ ok: false, headers, error: extractError(body, status) }),
        );
        response.on('error', (e: Error) => resolve({ ok: false, headers, error: e.message }));
        return;
      }

      const out = fs.createWriteStream(destPath);
      let settled = false;
      const fail = (msg: string) => {
        if (settled) return;
        settled = true;
        out.destroy();
        resolve({ ok: false, headers, error: msg });
      };
      out.on('error', (e) => fail(e.message));
      response.on('data', (chunk) => out.write(chunk));
      response.on('error', (e: Error) => fail(e.message));
      response.on('end', () =>
        out.end(() => {
          if (settled) return;
          settled = true;
          resolve({ ok: true, headers });
        }),
      );
    });

    request.on('error', (e) => resolve({ ok: false, headers: {}, error: e.message }));
    request.end();
  });
}

function extractError(body: string, status: number): string {
  try {
    const parsed = JSON.parse(body) as { message?: string; errorCode?: string };
    if (parsed.message) return parsed.message;
  } catch {
    // 非 JSON body（例如 HTML 錯誤頁）→ 落到通用訊息
  }
  return `下載失敗（HTTP ${status}）`;
}
