// spawn / kill .NET sidecar（Ceremony.Api）+ 動態 port + /health ready check。
// 連線資訊透過標準 ENV var 注入（ConnectionStrings__Ceremony 等），API code 不必改。
import { spawn, ChildProcess } from 'child_process';
import net from 'net';
import path from 'path';
import { app } from 'electron';
import { CeremonyConfig, buildConnectionString } from './config';

let proc: ChildProcess | null = null;

/** 找一個空閒 TCP port（取代 ESM-only 的 get-port，避免 CJS/ESM 相容問題）。 */
export function findFreePort(): Promise<number> {
  return new Promise((resolve, reject) => {
    const srv = net.createServer();
    srv.unref();
    srv.on('error', reject);
    srv.listen(0, '127.0.0.1', () => {
      const addr = srv.address();
      const port = typeof addr === 'object' && addr ? addr.port : 0;
      srv.close(() => resolve(port));
    });
  });
}

function resolveSidecar(): { cmd: string; args: string[]; cwd: string } {
  // cwd 必須設為含 appsettings.json 的資料夾：single-file exe 的 ContentRoot 預設取自
  // 工作目錄（非 exe 路徑），若不設，appsettings.json 不會被載入 → Backup:Directory 等
  // 設定全為 null（曾導致「資料備份」回 BACKUP_NOT_CONFIGURED 而失敗）。
  // 1) 明確指定 exe（測試 / 自訂）
  if (process.env.CEREMONY_SIDECAR_EXE) {
    const exe = process.env.CEREMONY_SIDECAR_EXE;
    return { cmd: exe, args: [], cwd: path.dirname(exe) };
  }
  // 2) 打包後：resources/api/Ceremony.Api.exe（cwd = 該資料夾，appsettings.json 同層）
  if (app.isPackaged) {
    const apiDir = path.join(process.resourcesPath, 'api');
    return { cmd: path.join(apiDir, 'Ceremony.Api.exe'), args: [], cwd: apiDir };
  }
  // 3) dev fallback：dotnet run 後端專案（cwd = 專案目錄；dotnet 亦以此為 ContentRoot）
  const proj = path.join(__dirname, '../../../backend/src/Ceremony.Api');
  return { cmd: 'dotnet', args: ['run', '--project', proj, '--no-launch-profile'], cwd: proj };
}

export interface StartResult {
  ok: boolean;
  apiBase?: string;
  port?: number;
  error?: string;
}

export async function startSidecar(cfg: CeremonyConfig): Promise<StartResult> {
  stopSidecar();

  const port = cfg.apiPort && cfg.apiPort > 0 ? cfg.apiPort : await findFreePort();
  const apiBase = `http://127.0.0.1:${port}/api/v1`;
  const { cmd, args, cwd } = resolveSidecar();
  const urlArg = `--urls=http://127.0.0.1:${port}`;
  // dotnet run 需要 `--` 才把後續當「應用程式參數」；直接 exe 則直接附加。
  const fullArgs = cmd === 'dotnet' ? [...args, '--', urlArg] : [...args, urlArg];

  let spawnError: string | null = null;

  proc = spawn(cmd, fullArgs, {
    env: {
      ...process.env,
      ConnectionStrings__Ceremony: buildConnectionString(cfg),
      ASPNETCORE_ENVIRONMENT: app.isPackaged
        ? 'Production'
        : process.env.ASPNETCORE_ENVIRONMENT ?? 'Development',
      // sidecar renderer 從 file:// 載入 → Origin 多為 "null"；dev 走 ng serve。
      Cors__AllowedOrigins__0: 'null',
      Cors__AllowedOrigins__1: 'file://',
      Cors__AllowedOrigins__2: 'http://localhost:4200',
      ...(cfg.jwtKey ? { Jwt__SigningKey: cfg.jwtKey } : {}),
    },
    cwd,
    stdio: ['ignore', 'pipe', 'pipe'],
    windowsHide: true,
  });

  proc.on('error', (e) => {
    spawnError = e.message;
  });
  proc.stdout?.on('data', (d) => process.stdout.write(`[api] ${d}`));
  proc.stderr?.on('data', (d) => process.stderr.write(`[api] ${d}`));

  const ready = await waitForReady(apiBase, 60_000, () => spawnError);
  if (!ready.ok) {
    stopSidecar();
    return { ok: false, error: ready.error };
  }
  return { ok: true, apiBase, port };
}

async function waitForReady(
  apiBase: string,
  timeoutMs: number,
  getSpawnError: () => string | null,
): Promise<{ ok: boolean; error?: string }> {
  const healthUrl = apiBase.replace(/\/api\/v1$/, '') + '/health';
  const deadline = Date.now() + timeoutMs;
  let lastErr = 'API 啟動逾時';
  let unhealthy = 0;

  while (Date.now() < deadline) {
    const spawnErr = getSpawnError();
    if (spawnErr) {
      return { ok: false, error: `無法啟動 sidecar：${spawnErr}（請確認已安裝 .NET 10 ASP.NET Core Runtime）` };
    }
    try {
      const res = await fetch(healthUrl);
      if (res.status === 200) return { ok: true }; // healthy：API + DB 皆 OK
      if (res.status === 503) {
        // 伺服器起來了但 DB 連不上 → 多半連線設定錯，重試數次仍失敗就回報。
        try {
          const body = (await res.json()) as { error?: string };
          lastErr = body?.error ?? '資料庫連線失敗';
        } catch {
          lastErr = '資料庫連線失敗';
        }
        if (++unhealthy >= 3) return { ok: false, error: lastErr };
      }
    } catch {
      // 連線被拒 → API 還在啟動，繼續等。
    }
    await delay(700);
  }
  return { ok: false, error: lastErr };
}

const delay = (ms: number) => new Promise<void>((r) => setTimeout(r, ms));

export function stopSidecar(): void {
  if (proc && !proc.killed) {
    try {
      proc.kill();
    } catch {
      // ignore
    }
  }
  proc = null;
}
