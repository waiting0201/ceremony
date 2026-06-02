// %APPDATA%/Ceremony/config.json 讀寫 + 連線字串組裝（sidecar 方案 C：純文字 JSON）。
// 決策見 docs/design/security.md「Sidecar 模式 DB 認證決策」、infrastructure.md「Sidecar 模式設定流程」。
import { app } from 'electron';
import { promises as fs } from 'fs';
import path from 'path';
import crypto from 'crypto';

export interface CeremonyConfig {
  dbHost: string;
  dbPort: number;
  dbName: string;
  dbUser: string;
  dbPassword: string;
  /** 0 / undefined = 每次啟動動態指派空閒 port */
  apiPort?: number;
  /** 每機隨機 JWT 簽章 key（首次寫入產生）；不入 repo、僅存本機 user profile */
  jwtKey?: string;
}

export function configDir(): string {
  return path.join(app.getPath('appData'), 'Ceremony');
}

export function configPath(): string {
  return path.join(configDir(), 'config.json');
}

/** 出廠預寫連線種子檔路徑：packaged 在 resources/，dev 在 build/。 */
function defaultConfigPath(): string {
  return app.isPackaged
    ? path.join(process.resourcesPath, 'default-config.json')
    : path.join(app.getAppPath(), 'build', 'default-config.json');
}

/**
 * 讀打包進安裝檔的出廠連線種子（build/default-config.json）。
 * 首次啟動無 %APPDATA% config.json 時，main 用它寫出 config 並跳過 /setup。
 * 缺檔 / 壞檔 / 無 dbHost → 回 null（main 退回 /setup 首次設定流程）。
 */
export async function readDefaultConfig(): Promise<Partial<CeremonyConfig> | null> {
  try {
    const raw = await fs.readFile(defaultConfigPath(), 'utf-8');
    const cfg = JSON.parse(raw) as Partial<CeremonyConfig>;
    return cfg.dbHost ? cfg : null;
  } catch {
    return null;
  }
}

export async function readConfig(): Promise<CeremonyConfig | null> {
  try {
    const raw = await fs.readFile(configPath(), 'utf-8');
    return JSON.parse(raw) as CeremonyConfig;
  } catch {
    return null; // 不存在 / 壞檔 → 視為首次啟動
  }
}

/** 寫入 config；缺 jwtKey 時自動產生並持久化（每機唯一，token 簽章一致）。 */
export async function writeConfig(cfg: CeremonyConfig): Promise<CeremonyConfig> {
  const withKey: CeremonyConfig = {
    ...cfg,
    jwtKey:
      cfg.jwtKey && cfg.jwtKey.length >= 32
        ? cfg.jwtKey
        : crypto.randomBytes(32).toString('base64'),
  };
  await fs.mkdir(configDir(), { recursive: true });
  await fs.writeFile(configPath(), JSON.stringify(withKey, null, 2), 'utf-8');
  return withKey;
}

/** 組 MSSQL 連線字串（對齊 infrastructure.md 範例）。 */
export function buildConnectionString(cfg: CeremonyConfig): string {
  const server = cfg.dbPort ? `${cfg.dbHost},${cfg.dbPort}` : cfg.dbHost;
  return (
    `Server=${server};Database=${cfg.dbName};User Id=${cfg.dbUser};Password=${cfg.dbPassword};` +
    `TrustServerCertificate=true;MultipleActiveResultSets=True`
  );
}
