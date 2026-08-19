// 決策 11 的送印路徑 —— 純函式部分（無 electron / fs / child_process 相依，可單元測試）。
//
// 背景：2026-08-15 現場實證——同一台 KYOCERA PA2000 GX、同一份 PDF，Adobe Reader 印得出來，
// 而 Chrome 與本程式都噴 0x80010105 且列印鈕呈灰色（＝完全印不出來）。
// ⇒ 印表機與驅動本身沒壞，壞的是「讀每使用者預設 DEVMODE → 轉 PrintTicket」那條路。
// Adobe 走的是「自帶一份設定 ＋ GDI 送印」的老路，與舊系統同機制——本路徑就是複刻它。
//
// ⚠️ 這一份與 print-form-core.ts 的 HELPER_RESULTS **刻意平行、不合併**：
// 那一份被 blockScope()（失敗印表機黑名單）、needsRestore()（還原 journal）、viewerTitle()
// 三個函式共用，語意全綁在「SetPrinter 寫每使用者預設 DEVMODE」那條路。把 printed / cancelled
// 塞進去，等於讓黑名單邏輯開始對一組它從沒設計過的值做判斷。
// （error 兩邊都有沒關係——parser 不同，不會互相汙染。）

/**
 * `Ceremony.PrintForm.exe print` 的 result 字彙。
 *
 * ⚠️ 跨語言契約：必須與 C# 的 `Ceremony.Domain.Reports.PrintDialogResults.All` 完全相同。
 * 兩邊沒有 codegen，各有一支「集合恰好等於這些值」的測試——改一邊就會讓另一邊紅。
 */
export const PRINT_RESULTS = [
  'printed',
  /** 使用者按了取消。**這不是錯誤**，不得顯示紅字。 */
  'cancelled',
  'no-default-printer',
  /**
   * PrintDlgW 自己失敗了（CommDlgExtendedError 非 0）——對話框根本沒開起來。
   * 現場出現它＝決策 11 的前提被推翻的訊號（我們挑舊版對話框的理由就是它不走 PrintTicket 轉換），
   * 要當成要查的訊號，不是重試的理由。
   */
  'dialog-failed',
  'render-failed',
  'driver-rejected',
  'error',
] as const;

export type PrintResult =
  | (typeof PRINT_RESULTS)[number]
  // 以下是 helper 根本沒跑（或我們自己擋下）的狀況，C# 端不會產生。
  | 'helper-missing'
  | 'helper-error'
  | 'skipped-not-windows'
  /** 已經有一個列印對話框開著。modal 疊 modal 只會讓現場搞不清楚在跟哪個講話。 */
  | 'helper-busy';

export interface PrintDialogOutcome {
  result: PrintResult;
  /** 對話框已經出現在螢幕上（helper 的第一行 NDJSON）。呼叫端就是靠它 resolve UI。 */
  shown?: boolean;
  formResult?: string;
  formTarget?: string;
  formKind?: number;
  devmodeSource?: string;
  pages?: number;
  pageCount?: number;
  copies?: number;
  range?: string;
  /**
   * spooler 的 job id（`StartDoc` 的回傳值）。
   *
   * 有它 ⇒ 這份工作**確實進了 Windows 列印佇列**，之後沒吐紙就不是本程式的範圍；
   * 沒有它 ⇒ 連 spooler 都沒收到。這一刀是 2026-08-18「印表機沒有反應」客訴的第一個分岔點。
   */
  jobId?: number;
  dpi?: string;
  printablePx?: string;
  physicalPx?: string;
  offsetPx?: string;
  destRect?: string;
  technology?: number;
  win32?: number;
  error?: string;
  ms?: number;
}

/** helper 的 print 子命令參數。 */
export function printDialogArgs(
  pdfPath: string,
  opts: {
    owner?: bigint | number | null;
    reportType?: string | null;
    noForm?: boolean;
    devmodeSource?: 'printer' | 'user' | 'none';
    scale?: 'fit' | 'stretch';
    jobName?: string | null;
  } = {},
): string[] {
  const args = ['print', pdfPath];
  if (opts.owner) args.push('--owner', String(opts.owner));
  if (opts.reportType) args.push('--report', opts.reportType);
  if (opts.noForm) args.push('--no-form');
  if (opts.devmodeSource) args.push('--devmode-source', opts.devmodeSource);
  if (opts.scale) args.push('--scale', opts.scale);
  if (opts.jobName) args.push('--job-name', opts.jobName);
  return args;
}

/**
 * 解析 helper 的一行 NDJSON。
 *
 * **永不丟例外**：驅動或 runtime 有時會往 stdout 吐與我們無關的雜訊，
 * 而這條路上唯一比「列印失敗」更糟的是「主行程因為一行看不懂的字而崩潰」。
 * 看不懂就回 null，呼叫端當那一行不存在。
 */
export function parsePrintLine(line: string): Partial<PrintDialogOutcome> | null {
  const trimmed = line.trim();
  if (!trimmed || !trimmed.startsWith('{')) return null;

  let parsed: unknown;
  try {
    parsed = JSON.parse(trimmed);
  } catch {
    return null;
  }
  if (typeof parsed !== 'object' || parsed === null) return null;

  const o = parsed as Record<string, unknown>;
  const out: Partial<PrintDialogOutcome> = {};

  if (o['event'] === 'dialog-shown') out.shown = true;

  if (typeof o['result'] === 'string') {
    // 未知的 result 一律退成 error 而不是原樣放行——否則 UI 會拿到一個沒人處理的字串。
    out.result = (PRINT_RESULTS as readonly string[]).includes(o['result'])
      ? (o['result'] as PrintResult)
      : 'error';
  }

  for (const k of ['formResult', 'formTarget', 'devmodeSource', 'range', 'dpi',
    'printablePx', 'physicalPx', 'offsetPx', 'destRect', 'error'] as const) {
    if (typeof o[k] === 'string') out[k] = o[k] as string;
  }
  for (const k of ['formKind', 'pages', 'pageCount', 'copies', 'technology', 'win32', 'ms',
    'jobId'] as const) {
    if (typeof o[k] === 'number') out[k] = o[k] as number;
  }

  return out;
}

/**
 * 寫進診斷紀錄的欄位（白名單）。
 *
 * ⚠️ **印表機的原始名稱一律不寫**——與既有 logFields 同一條規則（見 design/security.md）：
 * 那是使用者環境的識別資訊，診斷用雜湊或「default/other」就夠了。
 */
export function printDialogLogFields(o: PrintDialogOutcome): Record<string, unknown> {
  const f: Record<string, unknown> = { path: 'dialog', printResult: o.result };
  for (const k of ['formResult', 'formTarget', 'formKind', 'devmodeSource', 'pages', 'pageCount',
    'copies', 'range', 'dpi', 'printablePx', 'physicalPx', 'offsetPx', 'destRect',
    'technology', 'win32', 'error', 'ms', 'jobId'] as const) {
    if (o[k] !== undefined) f[k] = o[k];
  }
  return f;
}

/**
 * 給使用者看的訊息；null ＝ 不是錯誤，不要顯示任何東西。
 *
 * `cancelled` 回 null 是本函式存在的主要理由：使用者自己按取消卻跳一條紅字，
 * 是最容易讓人以為「程式壞了」的假警報。
 */
export function printDialogMessage(result: PrintResult): string | null {
  switch (result) {
    case 'printed':
    case 'cancelled':
      return null;
    case 'no-default-printer':
      return '找不到可用的印表機，請先在 Windows 設定一台';
    case 'helper-busy':
      return '已有列印對話框開著，請先完成或取消';
    case 'helper-missing':
    case 'skipped-not-windows':
      return '此電腦無法使用列印功能';
    case 'dialog-failed':
      return '無法開啟列印視窗，請改用「下載」後以 Adobe Reader 列印，並通知我們';
    case 'render-failed':
      return '報表內容讀取失敗，請重新產生後再試';
    case 'driver-rejected':
      return '印表機拒絕了這次列印，請改用「下載」後以 Adobe Reader 列印';
    default:
      return '列印失敗，請稍後再試';
  }
}

/**
 * 送印結束後（helper 行程退出）要給使用者看的那一行。`null` ＝ 什麼都不要顯示。
 *
 * **為什麼需要它**（2026-08-18 現場回報「按了列印，印表機沒有反應」）：
 * 這條路徑在 `dialog-shown` 就 resolve UI（決策 8 的守法），所以**對話框之後的一切結果
 * 原本只進診斷紀錄**——printed / driver-rejected / render-failed / error 在畫面上長得一模一樣，
 * 都是「什麼都沒發生」。現場只能回報「沒有反應」，而那四種的下一步完全不同。
 *
 * 這裡刻意把成功也講出來，而且措辭是「**已送出到印表機佇列**」不是「已列印」：
 * 我們能保證的只到 spooler 為止。這一刀正是現場最需要的——
 * 「我們沒送出去」與「送出去了但印表機沒吐紙」是兩條完全不同的排障路線。
 *
 * ⚠️ 顯示它**不得**讓 UI 進入等待狀態（列印鈕在 `dialog-shown` 就已經放開了）。
 * 這是事後告知，不是把 UI 綁回 spooler——後者正是決策 8 禁止的事。
 */
export function printDialogFinalMessage(o: PrintDialogOutcome): { ok: boolean; text: string } | null {
  if (o.result === 'cancelled') return null;

  if (o.result === 'printed') {
    const pages = typeof o.pages === 'number' && o.pages > 0 ? ` ${o.pages} 頁` : '';
    const job = typeof o.jobId === 'number' ? `（工作編號 ${o.jobId}）` : '';
    // 印出去了但紙沒自動選到，仍然要講——否則使用者要等紙印歪了才知道。
    const notice = printFormNoticeForDialog(o);
    const tail = notice ? `　⚠ ${notice}` : '';
    return {
      ok: true,
      text: `已送出${pages}到印表機佇列${job}。若印表機沒有動作，請開 Windows 的列印佇列看這筆工作的狀態${tail}`,
    };
  }

  // 失敗一律附上代碼：現場截一張圖就足以定位，不必再問一輪。
  const code = [o.result, o.win32 ? `win32=${o.win32}` : null].filter(Boolean).join(' ');
  return { ok: false, text: `${printDialogMessage(o.result) ?? '列印失敗'}（${code}）` };
}

/**
 * 自動選紙在**這條路徑**上的結果提示；`null` ＝ 不必說話。
 *
 * **為什麼需要**（2026-08-18 現場問「選對話框時也會自動選紙嗎？開著卻沒作用」）：
 * 會，但機制不同——舊路徑是開預覽視窗**之前**去寫每使用者預設 DEVMODE，
 * 新路徑是按下列印**當下**改我們自己帶進對話框的那份 copy（決策 11 的不變式：不碰共用狀態）。
 * 問題出在**回饋**：舊路徑選不到紙會把警語寫進預覽視窗標題（`viewerTitle`），
 * 新路徑的 `formResult` 卻只進診斷紀錄 ⇒ 使用者只看到「紙沒被選好」，完全不知道為什麼、
 * 也不知道該自己去對話框裡選。
 *
 * 文案沿用 `viewerTitle` 的原則：**不出現「DEVMODE」「PrintTicket」「驅動」這種字眼**，
 * 只講「現在該怎麼辦」。
 */
export function printFormNoticeForDialog(o: PrintDialogOutcome): string | null {
  // 沒有 formResult ＝ 這次根本沒要求選紙（使用者關掉自動選紙，或沒帶 reportType）。
  if (!o.formResult || o.formResult === 'exact') return null;

  const form = o.formTarget ? `「${o.formTarget}」` : '對應的';
  if (o.formResult === 'not-found') {
    return `這台印表機沒有${form}紙張設定，請在列印視窗自己選紙`;
  }
  if (o.formResult === 'mismatch') {
    return `紙張${form}的尺寸與報表不符、未自動選用（請依 IT 手冊重建），請在列印視窗自己選紙`;
  }
  if (o.formResult === 'skipped-virtual') {
    // PDF／XPS 之類的虛擬印表機本來就沒有實體紙匣，不是故障 ⇒ 不用嚇使用者。
    return null;
  }
  return `這台印表機無法自動選紙，請在列印視窗自己選${form}紙張`;
}
