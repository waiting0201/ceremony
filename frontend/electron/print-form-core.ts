// 列印前預選驅動自訂表單 —— 純函式部分（無 electron / fs / child_process 相依，可單元測試）。
//
// 為什麼要有這一層：見 docs/blueprints/print-channel-electron.md 決策 9。
// 舊系統在跳原生列印對話框前會用中文表單名比對驅動的紙張清單（SignupForm.cs:1770-1787），
// 新系統 v2.3.9 把這格拿掉了，於是六種報表都只能吃驅動的單一預設紙張。
// 真正的 Win32 呼叫在 Ceremony.PrintForm.exe，這裡只負責解析它的輸出與轉成 UI／log 用的形狀。

/**
 * helper 自己會回的結果；electron 端另有七種「helper 根本沒跑（或跑不完）」的結果，列在 FormResult。
 *
 * ⚠️ 這份清單是跨語言契約，與 C# 的 `PrinterFormPolicy.ToResult`、`PrintTicketPreflight.ToResult`
 * 以及 `PrinterContactPolicy` 的兩個常數一一對應
 * （少一個 → parseHelperOutput 把整包退成 helper-error）。
 */
const HELPER_RESULTS = [
  'exact',
  'mismatch',
  'not-found',
  'skipped-virtual',
  // 2026-08-08 起：寫入前先做一次 DEVMODE → PrintTicket 轉換，轉不過（或檢查跑不起來）就不寫。
  // 見 C# 的 PrintTicketPreflight 與 docs/blueprints/print-channel-electron.md 決策 9c。
  'skipped-printticket-reject',
  'skipped-printticket-unavailable',
  // 2026-08-10 起：碰過會出事的印表機不再接觸；呼叫端不等了就不寫入。
  // 見 C# 的 PrinterContactPolicy 與 blueprint 決策 9d。
  'skipped-printer-blocked',
  'skipped-over-budget',
  'unchanged',
  'restored',
  'no-default-printer',
  'driver-rejected',
  'error',
] as const;

export type FormResult =
  | (typeof HELPER_RESULTS)[number]
  | 'helper-missing'
  | 'helper-error'
  | 'helper-timeout'
  | 'skipped-not-windows'
  | 'skipped-viewer-open'
  // 使用者自己在報表頁關掉了自動選紙（＝現場不必改環境變數就能止血的那個開關）
  | 'skipped-disabled'
  // 上一次的 helper 還沒結束。逾時後我們不再 kill 它（見 print-form.ts 的 run），
  // 所以要有一格表示「不疊第二個驅動呼叫上去」。
  | 'skipped-helper-busy';

/** DEVMODE 裡被我們動過的那幾格 + 印表機名稱，用於還原。 */
export interface RestoreSnapshot {
  kind: number;
  fields: number;
  w: number;
  h: number;
  /** 原始印表機名稱。**只寫還原 journal，不寫診斷紀錄**（見 logFields）。 */
  printer?: string;
}

export interface FormApplyResult {
  result: FormResult;
  /** 我們想要的表單名（不論找到與否）。 */
  form?: string;
  /** 命中的驅動表單 ID（dmPaperSize）。 */
  kind?: number;
  printerHash?: string;
  virtual?: boolean;
  /** 尺寸差，例 "-8.32x-5.76"（mm）。 */
  mismatchMm?: string;
  ms?: number;
  prev?: RestoreSnapshot;
  error?: string;
}

const SKIPPED_NOT_WINDOWS: FormApplyResult = { result: 'skipped-not-windows' };

export function skippedNotWindows(): FormApplyResult {
  return SKIPPED_NOT_WINDOWS;
}

/**
 * 解析 helper 的 stdout。**任何非預期輸入都回 helper-error，永不丟例外。**
 *
 * helper 的 exit code 一律是 0，成敗只看這裡的 result；所以這個函式是唯一的判讀點，
 * 壞掉的話整條列印會跟著壞——它必須是全域最保守的那一段程式。
 */
export function parseHelperOutput(stdout: string): FormApplyResult {
  try {
    // 取最後一行非空輸出：驅動有機會往 stdout 吐東西，我們自己的 JSON 一定在最後。
    const line = stdout
      .split('\n')
      .map((s) => s.trim())
      .filter(Boolean)
      .pop();
    if (!line) return { result: 'helper-error', error: 'empty output' };

    const raw = JSON.parse(line) as Record<string, unknown>;
    const result = raw['result'];
    if (typeof result !== 'string' || !(HELPER_RESULTS as readonly string[]).includes(result)) {
      return { result: 'helper-error', error: `unexpected result: ${String(result)}` };
    }

    const out: FormApplyResult = { result: result as FormResult };
    if (typeof raw['form'] === 'string') out.form = raw['form'];
    if (typeof raw['kind'] === 'number') out.kind = raw['kind'];
    if (typeof raw['printerHash'] === 'string') out.printerHash = raw['printerHash'];
    if (typeof raw['virtual'] === 'boolean') out.virtual = raw['virtual'];
    if (typeof raw['ms'] === 'number') out.ms = raw['ms'];
    if (typeof raw['error'] === 'string') out.error = raw['error'];

    const mismatch = raw['mismatchMm'] as { w?: unknown; h?: unknown } | undefined;
    if (mismatch && typeof mismatch.w === 'number' && typeof mismatch.h === 'number') {
      out.mismatchMm = `${mismatch.w}x${mismatch.h}`;
    }

    const prev = raw['prev'] as Record<string, unknown> | undefined;
    if (
      prev &&
      typeof prev['kind'] === 'number' &&
      typeof prev['fields'] === 'number' &&
      typeof prev['w'] === 'number' &&
      typeof prev['h'] === 'number'
    ) {
      out.prev = {
        kind: prev['kind'],
        fields: prev['fields'],
        w: prev['w'],
        h: prev['h'],
        ...(typeof prev['printer'] === 'string' ? { printer: prev['printer'] } : {}),
      };
    }

    return out;
  } catch (e) {
    return { result: 'helper-error', error: (e as Error).message };
  }
}

/**
 * 這次的結果要不要讓我們「以後不再碰」，以及範圍多大。
 *
 * - `printer`：這台印表機（用 printerHash 記帳）——驅動已經明確給過答案，再問一百次也一樣，
 *   而每問一次就多一次把 v4 驅動的設定模組叫起來的機會（2026-08-10 卡死客訴的來源）。
 * - `all`：整台機器停用預選——**逾時／helper 壞掉時我們不知道是哪台印表機**，
 *   而逾時正是最該停手的訊號（代表有個驅動呼叫卡在那裡）。寧可整台停掉自動選紙。
 * - `none`：驅動好好回答了（找不到同名表單、尺寸不符、虛擬印表機…），那是健康的失敗。
 *
 * ⚠️ `unchanged` / `exact` 不在此列：那是成功。`skipped-*`（我們自己決定跳過的）也不記帳，
 * 否則「另一個視窗開著」這種暫時狀態會被誤記成永久黑名單。
 */
export type BlockScope = 'none' | 'printer' | 'all';

const DRIVER_FAULT_RESULTS: readonly FormResult[] = [
  'skipped-printticket-reject',
  'skipped-printticket-unavailable',
  'driver-rejected',
  'error',
];

export function blockScope(r: FormApplyResult): BlockScope {
  if (r.result === 'helper-timeout' || r.result === 'helper-error') return 'all';
  if (!DRIVER_FAULT_RESULTS.includes(r.result)) return 'none';
  // 沒有 hash 就記不到那台頭上（helper 在拿到印表機名稱之前就失敗）——退成整台停用，
  // 不是不記：記不到又不停用，等於這道閘門對這條路徑完全失效。
  return r.printerHash ? 'printer' : 'all';
}

/**
 * helper 的 apply 子命令參數。
 *
 * `--budget-ms` 與 `--blocked` 的語意見 C# 的 `PrinterContactPolicy`：前者讓 helper 自己守住
 * 「呼叫端不等了就不寫入」（因為我們不再中途 kill 它），後者讓它在**任何驅動呼叫之前**就結束。
 */
export function applyArgs(
  reportType: string,
  budgetMs: number,
  blocked: readonly string[],
): string[] {
  const args = ['apply', reportType, '--budget-ms', String(budgetMs)];
  if (blocked.length > 0) args.push('--blocked', blocked.join(','));
  return args;
}

const DEFAULT_TITLE = '列印預覽 — 請按工具列的列印鈕';

/**
 * 檢視器視窗標題。**兩種「現場設定沒做對」的情況要讓使用者看得到**——
 * 標題列已經是既有的說明文字載體，不擋流程也不需要新 UI。
 *
 * 其餘結果（helper 沒跑起來、驅動拒絕、非 Windows）不警告：那不是使用者能處理的事，
 * 而且列印本身照常可用。
 */
export function viewerTitle(r: FormApplyResult): string {
  if (r.result === 'mismatch') {
    // 2026-08-06 起尺寸不符不再自動選（見 C# 的 PrinterFormPolicy），所以文案要同時講
    // 「沒幫你選」與「怎麼自救」——只說「請重建」會讓使用者以為這次還是選好了。
    return `列印預覽 — ⚠ 紙張「${r.form}」尺寸不符、未自動選用（請依 IT 手冊重建），請在列印對話框手動選紙`;
  }
  if (r.result === 'not-found') {
    return `列印預覽 — ⚠ 印表機沒有「${r.form}」紙張設定，請在列印對話框手動選紙`;
  }
  if (
    r.result === 'skipped-printticket-reject' ||
    r.result === 'skipped-printticket-unavailable' ||
    r.result === 'skipped-over-budget'
  ) {
    // 使用者能做的事跟 not-found 一樣（手動選紙），所以不必區分；差別只寫進診斷紀錄。
    // 刻意不出現「PrintTicket」「驅動」「逾時」這種字眼——現場要看到的是「我現在該怎麼辦」。
    return `列印預覽 — ⚠ 這台印表機不支援自動選紙，請在列印對話框手動選「${r.form}」`;
  }
  if (r.result === 'skipped-printer-blocked') {
    // 這台印表機曾經在自動選紙時出事，我們已經永久不碰它（決策 9d）。這裡沒有表單名可講——
    // helper 在比對之前就結束了，那正是重點。
    return '列印預覽 — ⚠ 這台印表機已停用自動選紙，請在列印對話框手動選紙';
  }
  if (r.result === 'skipped-disabled') {
    // 使用者自己關的，不是異常 ⇒ 不掛 ⚠，但仍要說明為什麼紙沒被選好。
    return '列印預覽 — 自動選紙已關閉，請在列印對話框手動選紙';
  }
  if (r.result === 'skipped-viewer-open' || r.result === 'skipped-helper-busy') {
    return '列印預覽 — ⚠ 另一個列印視窗開著，本次未自動選紙，請在列印對話框手動選紙';
  }
  return DEFAULT_TITLE;
}

/**
 * 併進診斷紀錄那一行 JSON 的欄位。
 *
 * ⚠️ 這是一份**白名單**，不是把 result 攤平——`prev.printer` 是印表機原始名稱，
 * 現場常見形式是 `\\PC-王小明\HP LaserJet 1020`，等於同時洩漏使用者姓名與內網主機名，
 * 而診斷紀錄是會被使用者整份傳回來的。要辨識機器用 printerHash 就夠了。
 * 見 docs/design/security.md。
 */
export function logFields(r: FormApplyResult): Record<string, unknown> {
  const out: Record<string, unknown> = { formResult: r.result };
  if (r.form !== undefined) out['formTarget'] = r.form;
  if (r.kind !== undefined) out['formKind'] = r.kind;
  if (r.mismatchMm !== undefined) out['formMismatchMm'] = r.mismatchMm;
  if (r.ms !== undefined) out['formMs'] = r.ms;
  if (r.virtual !== undefined) out['printerVirtual'] = r.virtual;
  if (r.printerHash !== undefined) out['printerHash'] = r.printerHash;
  if (r.error !== undefined) out['formError'] = r.error;
  return out;
}

/**
 * 只有真的動到驅動設定才需要記還原快照。
 *
 * `exact` 是唯一會寫入的結果——2026-08-06 起 `mismatch` 與 `skipped-virtual` 都只回報不寫入
 * （見 C# 的 `PrinterFormPolicy`）。`unchanged`／`not-found`／各種失敗本來就沒動過。
 * helper 在不寫入時不回 `prev`，所以這裡的 `prev` 檢查是第二道保險，不是主要判準。
 */
export function needsRestore(r: FormApplyResult): boolean {
  return r.result === 'exact' && r.prev !== undefined;
}

/** helper 的 restore 子命令參數。 */
export function restoreArgs(s: RestoreSnapshot): string[] {
  const args = ['restore', String(s.kind), String(s.fields), String(s.w), String(s.h)];
  if (s.printer) args.push(s.printer);
  return args;
}
