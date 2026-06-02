// 開機偵測 client 必要安裝的軟體：VC++ Redistributable + .NET 10 ASP.NET Core Runtime。
// 缺了由前端 /prereq 頁引導安裝（launchInstaller / openExternal）。
// 非 Windows（dev on macOS/Linux）→ 略過偵測，回 ok（sidecar 走 dotnet run，不需 client runtime）。
import { execFile } from 'child_process';
import { promisify } from 'util';

const pexec = promisify(execFile);

export type PrereqKey = 'vcredist' | 'dotnet';

export interface PrereqItem {
  key: PrereqKey;
  name: string;
  ok: boolean;
  detail?: string;
  /** 官方下載頁（缺 bundle installer 時 fallback 開此連結） */
  downloadUrl: string;
  /** 若安裝包有 bundle 於 resources/prereqs，檔名用於直接執行 */
  installerFile?: string;
}

export interface PrereqReport {
  ok: boolean;
  skipped: boolean;
  platform: string;
  items: PrereqItem[];
}

const VC_URL = 'https://aka.ms/vs/17/release/vc_redist.x64.exe';
const DOTNET_URL = 'https://dotnet.microsoft.com/download/dotnet/10.0/runtime';
const VC_NAME = 'Microsoft Visual C++ 2015-2022 Redistributable (x64)';
const DOTNET_NAME = '.NET 10 ASP.NET Core Runtime (x64)';

export async function detectPrereqs(): Promise<PrereqReport> {
  if (process.platform !== 'win32') {
    return {
      ok: true,
      skipped: true,
      platform: process.platform,
      items: [
        { key: 'vcredist', name: VC_NAME, ok: true, detail: '非 Windows，略過偵測', downloadUrl: VC_URL },
        { key: 'dotnet', name: DOTNET_NAME, ok: true, detail: '非 Windows，略過偵測', downloadUrl: DOTNET_URL },
      ],
    };
  }

  const [vc, dotnet] = await Promise.all([detectVcRedist(), detectDotnet()]);
  return { ok: vc.ok && dotnet.ok, skipped: false, platform: 'win32', items: [vc, dotnet] };
}

async function detectVcRedist(): Promise<PrereqItem> {
  // VC++ 2015-2022 (14.x) runtime 在 registry 留 Installed=1 + Version。
  const keys = [
    'HKLM\\SOFTWARE\\Microsoft\\VisualStudio\\14.0\\VC\\Runtimes\\x64',
    'HKLM\\SOFTWARE\\WOW6432Node\\Microsoft\\VisualStudio\\14.0\\VC\\Runtimes\\x64',
  ];
  for (const k of keys) {
    try {
      const { stdout } = await pexec('reg', ['query', k, '/v', 'Installed']);
      if (/Installed\s+REG_DWORD\s+0x1/i.test(stdout)) {
        return { key: 'vcredist', name: VC_NAME, ok: true, detail: '已安裝', downloadUrl: VC_URL, installerFile: 'vc_redist.x64.exe' };
      }
    } catch {
      // key 不存在 → 試下一個
    }
  }
  return {
    key: 'vcredist',
    name: VC_NAME,
    ok: false,
    detail: '未偵測到（SkiaSharp 列印直書/地址需要）',
    downloadUrl: VC_URL,
    installerFile: 'vc_redist.x64.exe',
  };
}

async function detectDotnet(): Promise<PrereqItem> {
  try {
    const { stdout } = await pexec('dotnet', ['--list-runtimes']);
    const has10 = stdout
      .split(/\r?\n/)
      .some((l) => /^Microsoft\.AspNetCore\.App\s+10\./.test(l.trim()));
    if (has10) {
      return { key: 'dotnet', name: DOTNET_NAME, ok: true, detail: '已安裝', downloadUrl: DOTNET_URL, installerFile: 'aspnetcore-runtime-10-win-x64.exe' };
    }
    return {
      key: 'dotnet',
      name: DOTNET_NAME,
      ok: false,
      detail: '已裝 .NET 但缺 ASP.NET Core Runtime 10.x',
      downloadUrl: DOTNET_URL,
      installerFile: 'aspnetcore-runtime-10-win-x64.exe',
    };
  } catch {
    return {
      key: 'dotnet',
      name: DOTNET_NAME,
      ok: false,
      detail: '未偵測到 .NET（找不到 dotnet 指令）',
      downloadUrl: DOTNET_URL,
      installerFile: 'aspnetcore-runtime-10-win-x64.exe',
    };
  }
}
