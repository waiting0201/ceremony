// 送印選項：丟給 webContents.print 的完整 options。
//
// 三個獨立的軸，預設值全部是 'driver'ㄧ「什麼都不指定」，把紙張 / 邊界 / 縮放 / 方向
// 交回印表機驅動的 DEVMODE。三個都是 'driver' 時，送出的物件只有四個 key。
//
// 為什麼預設是 driver（2026-08-01 客訴「跟之前我們調好的位置都跑掉了」）：
// v2.3.7 的預設是 margins:'none' + scaleFactor:100 + pageSize（微米自訂紙），一次改了三件事。
// 而 docs/blueprints/printing-reports-positions.md 那套 ±0.05cm 的欄位座標，全部是在
// 「PDF 檢視器按列印 → 原生對話框」的基準（＝三軸皆 driver）下實機驗收的——座標表沒有記錄
// 它的驗收前提，送印路徑一換整份就作廢。其中 margins:'none' 把版面推到實體紙緣，而印表機有
// 0.3–0.5cm 不可列印邊界，量級與 docs/gotchas.md「不可列印邊界整欄吃掉 Left<0.5cm 的欄位」相同。
//
// 為什麼仍然給使用者選：我們無法在 macOS 上證明 driver 等價於改版前，而現場的印表機、驅動、
// 自訂紙張各不相同。把三個軸攤開來，任何一台機器需要別的組合時使用者能自救——
// 而不是把全部風險押在一個沒驗證過的假設上。每種報表各自記住自己的選擇。
//
// 對照表：v2.3.7 的「實際大小」= { scale:'actual', paper:'report' }，仍可手動選回來。
//
// 刻意不 import 'electron'：純函式才測得到（比照 paper.ts）。

/** 'driver' = 不指定（交回驅動）；'actual' = 100% 不縮放、邊界 0；'fit' = 縮到可列印範圍。 */
export type ScaleMode = 'driver' | 'actual' | 'fit';
/** 'driver' = 不指定（交回驅動 DEVMODE 的 dmOrientation）。 */
export type OrientationMode = 'driver' | 'portrait' | 'landscape';
/** 'driver' = 用驅動當前紙張；'report' = 指定報表尺寸（需驅動支援該自訂尺寸）。 */
export type PaperMode = 'driver' | 'report';

export interface PageSizeMicrons {
  width: number;
  height: number;
}

export interface PrintModes {
  scale?: ScaleMode;
  orientation?: OrientationMode;
  paper?: PaperMode;
}

export interface PrintOptionsInput extends PrintModes {
  copies: number;
  deviceName?: string;
  /** paper:'report' 時要用的尺寸；沒有就退回驅動紙張。 */
  pageSize?: PageSizeMicrons | null;
}

/** Electron WebContentsPrintOptions 的子集；型別在此重述，避免本檔 import 'electron'。 */
export interface BuiltPrintOptions {
  silent: true;
  printBackground: true;
  copies: number;
  deviceName?: string;
  margins?: { marginType: 'none' | 'printableArea' };
  scaleFactor?: number;
  landscape?: boolean;
  pageSize?: PageSizeMicrons;
}

export function buildPrintOptions(o: PrintOptionsInput): BuiltPrintOptions {
  const scale = o.scale ?? 'driver';
  const orientation = o.orientation ?? 'driver';
  const paper = o.paper ?? 'driver';

  return {
    silent: true,
    printBackground: true,
    copies: o.copies,
    // 每個分支都刻意用展開而不是賦值 undefined：Electron 會把「有這個 key 但值是 undefined」
    // 當成有指定，等於偷偷覆寫 DEVMODE——那正是這次客訴的形狀。
    ...(o.deviceName ? { deviceName: o.deviceName } : {}),
    ...(scale === 'actual' ? { margins: { marginType: 'none' as const }, scaleFactor: 100 } : {}),
    ...(scale === 'fit' ? { margins: { marginType: 'printableArea' as const } } : {}),
    ...(orientation === 'driver' ? {} : { landscape: orientation === 'landscape' }),
    ...(paper === 'report' && o.pageSize ? { pageSize: o.pageSize } : {}),
  };
}
