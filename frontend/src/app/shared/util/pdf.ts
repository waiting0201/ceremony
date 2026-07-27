/**
 * 產出的報表 PDF 一律開新分頁預覽（使用者再自行列印 / 另存）。
 * blob URL 於 60 秒後回收——分頁載入後仍持有內容，回收只是避免記憶體長期佔用。
 */
export function openPdfInNewTab(blob: Blob): void {
  const url = URL.createObjectURL(blob);
  window.open(url, '_blank', 'noopener');
  setTimeout(() => URL.revokeObjectURL(url), 60_000);
}
