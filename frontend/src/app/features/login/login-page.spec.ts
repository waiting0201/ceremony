import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { LoginPage } from './login-page';

/**
 * 回歸鎖（2026-08-11 客訴「登入頁的 logo 圖破了」）：靜態資源不得寫成絕對路徑。
 *
 * 桌面版是 `mainWindow.loadFile(...)` ＝ `file:` protocol，打包用 `--base-href ./`；
 * 絕對路徑**不吃 base href**，`/logo.png` 會被解成 `file:///logo.png`（磁碟根目錄）→ 破圖。
 * `ng serve` 的 root 剛好是 `/` 所以本機永遠看不出來，只能靠這條鎖住。
 * 見 docs/gotchas.md〈桌面版走 `file://`，模板裡的絕對路徑一定破圖〉。
 */
describe('LoginPage（靜態資源路徑）', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });
  });

  it('logo 走相對路徑，不以 / 開頭（桌面版 file:// 才不會破圖）', () => {
    const fixture = TestBed.createComponent(LoginPage);
    fixture.detectChanges();

    const img: HTMLImageElement | null = fixture.nativeElement.querySelector('img.gate-logo');
    expect(img).not.toBeNull();
    expect(img!.getAttribute('src')).toBe('logo.png');
  });

  it('整頁沒有任何以 / 開頭的資源路徑', () => {
    const fixture = TestBed.createComponent(LoginPage);
    fixture.detectChanges();

    const abs = [...fixture.nativeElement.querySelectorAll('[src]')].filter(
      (el) => (el as Element).getAttribute('src')?.startsWith('/') ?? false,
    );
    expect(abs).toEqual([]);
  });
});
