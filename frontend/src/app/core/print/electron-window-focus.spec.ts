import type { BrowserWindow } from 'electron';
import { returnFocusOnClose } from '../../../../electron/window-focus';

/**
 * electron/window-focus.ts 的行為測試（spec 放 src/ 是因為 unit-test builder 只掃 src/）。
 *
 * 鎖的是「什麼時候**不**搶焦點」——搶過頭會在使用者切去別的程式時把畫面拉回來，比原本的
 * bug 更煩人，而那條路徑在 Windows 實機上很難重現。
 */
describe('window-focus', () => {
  interface FakeWin {
    on(event: string, fn: () => void): void;
    isFocused(): boolean;
    isDestroyed(): boolean;
    isMinimized(): boolean;
    focus(): void;
  }

  function fakeChild(focusedOnClose: boolean) {
    const handlers: Record<string, () => void> = {};
    const win = {
      on: (event: string, fn: () => void) => void (handlers[event] = fn),
      isFocused: () => focusedOnClose,
    } as unknown as BrowserWindow;
    return { win, close: () => handlers['close']?.(), closed: () => handlers['closed']?.() };
  }

  function fakeParent(state: { destroyed?: boolean; minimized?: boolean } = {}) {
    const calls = { focus: 0 };
    const win = {
      isDestroyed: () => state.destroyed === true,
      isMinimized: () => state.minimized === true,
      focus: () => void calls.focus++,
    } as unknown as BrowserWindow;
    return { win, calls };
  }

  it('預覽視窗自己持有焦點時關閉 → 焦點交還主視窗', () => {
    const child = fakeChild(true);
    const parent = fakeParent();

    returnFocusOnClose(child.win, parent.win);
    child.close();
    child.closed();

    expect(parent.calls.focus).toBe(1);
  });

  it('使用者已切到別的程式（預覽沒有焦點）→ 不搶回來', () => {
    const child = fakeChild(false);
    const parent = fakeParent();

    returnFocusOnClose(child.win, parent.win);
    child.close();
    child.closed();

    expect(parent.calls.focus).toBe(0);
  });

  it('主視窗最小化 → 不把它叫起來', () => {
    const child = fakeChild(true);
    const parent = fakeParent({ minimized: true });

    returnFocusOnClose(child.win, parent.win);
    child.close();
    child.closed();

    expect(parent.calls.focus).toBe(0);
  });

  it('主視窗已銷毀（先關主視窗再關預覽）→ 不碰它', () => {
    const child = fakeChild(true);
    const parent = fakeParent({ destroyed: true });

    returnFocusOnClose(child.win, parent.win);
    child.close();
    child.closed();

    expect(parent.calls.focus).toBe(0);
  });

  it('沒有 parent（獨立視窗）→ 不註冊任何 hook，也不丟例外', () => {
    const registered: string[] = [];
    const child = { on: (e: string) => void registered.push(e) } as unknown as BrowserWindow;

    returnFocusOnClose(child, null);

    expect(registered).toEqual([]);
  });
});
