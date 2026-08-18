// 決策 11 預覽視窗的 wrapper 頁 —— 純字串產生器（無 electron 相依，可單元測試）。
//
// 為什麼是「執行時產生到 temp」而不是打包一份 .html：
// 那份 HTML 要引用同一個 temp 目錄裡的 PDF，做成靜態檔就得處理 file:// 的相對路徑、
// asar 內外差異與打包步驟。產生一份丟在 PDF 旁邊，生命週期與 PDF 完全一致
//（同一個 `closed` hook 一起刪），是最少活動零件的作法。

/** 用在 HTML 文字節點與屬性值的逃脫。PDF 路徑含使用者名稱，可能有任何字元。 */
function escapeHtml(s: string): string {
  return s
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&#39;');
}

/**
 * 把本機路徑轉成 file:// URL。
 *
 * ⚠️ 中文使用者名稱是常態（`C:\Users\王小明\…`），一定要 encode；
 * 反斜線也要換成正斜線，否則 Chromium 會把 `\` 當成路徑分隔以外的東西。
 */
export function toFileUrl(p: string): string {
  const normalized = p.replace(/\\/g, '/');
  const withSlash = normalized.startsWith('/') ? normalized : `/${normalized}`;
  return `file://${encodeURI(withSlash).replace(/[?#]/g, (c) => encodeURIComponent(c))}`;
}

/**
 * 產生預覽頁。
 *
 * 版面刻意極簡：一條工具列 ＋ 佔滿其餘空間的 PDF。它的職責只有「讓使用者在送印前看到內容」，
 * 與舊系統的 `PrintPreviewDialog` 對等。
 *
 * ⚠️ PDF 的 iframe 帶 `#toolbar=0`：**這是對舊路徑「刻意不加 toolbar=0」那行註解的明確推翻**。
 * 在這條路徑上，Chromium 那顆 🖨 會通往 Windows 新版對話框——也就是已知會壞的那一條。
 * 同一個動作有兩個入口而語意不同，比少一顆按鈕糟得多。
 */
export function viewerPageHtml(pdfPath: string, title: string): string {
  const src = `${toFileUrl(pdfPath)}#toolbar=0&navpanes=0`;

  return `<!doctype html>
<html lang="zh-Hant">
<head>
<meta charset="utf-8">
<title>${escapeHtml(title)}</title>
<style>
  :root { color-scheme: light; }
  * { box-sizing: border-box; }
  html, body { margin: 0; height: 100%; font-family: "Microsoft JhengHei", "PingFang TC", sans-serif; }
  body { display: flex; flex-direction: column; background: #f3f3f3; }
  .bar {
    display: flex; align-items: center; gap: 12px;
    padding: 10px 14px; background: #fff; border-bottom: 1px solid #d8d8d8;
  }
  .grow { flex: 1; }
  button {
    font: inherit; font-size: 15px; padding: 8px 20px; border-radius: 6px;
    border: 1px solid #c4c4c4; background: #fff; cursor: pointer;
  }
  button:hover { background: #f0f0f0; }
  .primary { background: #b5654a; border-color: #b5654a; color: #fff; font-weight: 600; }
  .primary:hover { background: #a25a42; }
  .primary:disabled { background: #ccc; border-color: #ccc; cursor: default; }
  .msg { color: #b00; font-size: 14px; }
  .msg.ok { color: #4a7a4a; }
  iframe { flex: 1; width: 100%; border: 0; background: #525659; }
</style>
</head>
<body>
  <div class="bar">
    <button class="primary" id="print">列印</button>
    <span class="msg" id="msg"></span>
    <span class="grow"></span>
    <button id="close">關閉</button>
  </div>
  <iframe src="${escapeHtml(src)}"></iframe>
<script>
  var btn = document.getElementById('print');
  var msg = document.getElementById('msg');
  btn.addEventListener('click', function () {
    msg.textContent = '';
    msg.className = 'msg';   // 上一次的成功訊息（綠字）不能留到這一次
    btn.disabled = true;
    // 回來的時機是「對話框已經在螢幕上」，不是「印完了」——所以馬上就能再按下一次。
    window.ceremonyViewer.print().then(function (r) {
      btn.disabled = false;
      if (!r.ok && r.error) msg.textContent = r.error;
    }).catch(function () {
      btn.disabled = false;
      msg.textContent = '列印失敗，請稍後再試';
    });
  });
  document.getElementById('close').addEventListener('click', function () {
    window.ceremonyViewer.close();
  });
  // 送印結束後的結果（成功也講）。**不動 btn.disabled**——列印鈕在對話框出現時就放開了，
  // 這裡只是把「我們送到哪一步」寫在畫面上，讓現場不必再回報「沒有反應」。
  window.ceremonyViewer.onResult(function (r) {
    msg.textContent = r.text;
    msg.className = r.ok ? 'msg ok' : 'msg';
  });
</script>
</body>
</html>`;
}
