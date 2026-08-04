// 列印前預選驅動自訂表單 —— 純函式部分（無 electron / fs / child_process 相依，可單元測試）。
//
// 為什麼要有這一層：見 docs/blueprints/print-channel-electron.md 決策 9。
// 舊系統在跳原生列印對話框前會用中文表單名比對驅動的紙張清單（SignupForm.cs:1770-1787），
// 新系統 v2.3.9 把這格拿掉了，於是六種報表都只能吃驅動的單一預設紙張。
// 真正的 Win32 呼叫在 Ceremony.PrintForm.exe，這裡只負責解析它的輸出與轉成 UI／log 用的形狀。

/** helper 自己會回的結果；電子端另有四種「helper 根本沒跑成功」的結果。 */
const HELPER_RESULTS = [
  'exact',
  'mismatch',
  'not-found',
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
  | 'skipped-not-windows';

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
    return `列印預覽 — ⚠ 紙張「${r.form}」尺寸不符（請依 IT 手冊重建），請按工具列的列印鈕`;
  }
  if (r.result === 'not-found') {
    return `列印預覽 — ⚠ 印表機沒有「${r.form}」紙張設定，請在列印對話框手動選紙`;
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

/** 只有真的動到驅動設定才需要記還原快照（unchanged / not-found / 失敗都不必）。 */
export function needsRestore(r: FormApplyResult): boolean {
  return (r.result === 'exact' || r.result === 'mismatch') && r.prev !== undefined;
}

/** helper 的 restore 子命令參數。 */
export function restoreArgs(s: RestoreSnapshot): string[] {
  const args = ['restore', String(s.kind), String(s.fields), String(s.w), String(s.h)];
  if (s.printer) args.push(s.printer);
  return args;
}
