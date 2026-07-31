/**
 * jsdom 的 Blob 沒有 text() / arrayBuffer()（真實 Chromium 有），
 * 但列印通道與 blob 錯誤解析都靠它們 → 測試環境補上，否則只能繞過去測假的東西。
 *
 * 在需要的 spec 的 beforeEach 呼叫；重複呼叫是安全的。
 */
export function installBlobPolyfill(): void {
  const proto = Blob.prototype as Blob & {
    text?: () => Promise<string>;
    arrayBuffer?: () => Promise<ArrayBuffer>;
  };

  const readAs = <T>(blob: Blob, kind: 'text' | 'buffer'): Promise<T> =>
    new Promise((resolve, reject) => {
      const reader = new FileReader();
      reader.onerror = () => reject(reader.error);
      reader.onload = () => resolve(reader.result as T);
      if (kind === 'text') reader.readAsText(blob);
      else reader.readAsArrayBuffer(blob);
    });

  if (typeof proto.text !== 'function') {
    proto.text = function (this: Blob) {
      return readAs<string>(this, 'text');
    };
  }
  if (typeof proto.arrayBuffer !== 'function') {
    proto.arrayBuffer = function (this: Blob) {
      return readAs<ArrayBuffer>(this, 'buffer');
    };
  }
}
