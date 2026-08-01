import { ComponentFixture, TestBed } from '@angular/core/testing';
import { PrintDialogComponent } from './print-dialog.component';
import type { PrintDialogConfig, PrintDialogResult } from './print-dialog.types';

describe('PrintDialogComponent', () => {
  const baseConfig: PrintDialogConfig = {
    reportLabel: '資料卡',
    paperLabel: '21 × 14.8 cm',
    printers: [{ name: 'HP-1', displayName: 'HP LaserJet', isDefault: true, status: 0 }],
    copies: 1,
    scale: 'driver',
    orientation: 'driver',
    paper: 'driver',
    mode: 'printer',
    previewUrl: null,
  };

  async function open(
    patch: Partial<PrintDialogConfig> = {},
  ): Promise<ComponentFixture<PrintDialogComponent>> {
    const f = TestBed.createComponent(PrintDialogComponent);
    f.componentRef.setInput('config', { ...baseConfig, ...patch });
    f.detectChanges();
    await f.whenStable();
    f.detectChanges();
    return f;
  }

  const el = (f: ComponentFixture<PrintDialogComponent>) => f.nativeElement as HTMLElement;
  const text = (f: ComponentFixture<PrintDialogComponent>) => el(f).textContent ?? '';

  beforeEach(() => TestBed.configureTestingModule({ imports: [PrintDialogComponent] }));

  it('有 previewUrl → 渲染 iframe，並用 #toolbar=0 擋掉 PDF viewer 自己的列印鈕', async () => {
    const f = await open({ previewUrl: 'blob:abc' });
    const iframe = el(f).querySelector('iframe.pdf-frame');

    expect(iframe).not.toBeNull();
    // 內建工具列的列印鈕會繞過整條列印通道（紙張 / 縮放全失效）
    expect(iframe!.getAttribute('src')).toBe('blob:abc#toolbar=0');
    expect(el(f).querySelector('.print-dialog')!.classList).not.toContain('no-preview');
  });

  it('無預覽 → 不渲染 iframe，改顯示原因並退回窄版', async () => {
    const f = await open({ previewNotice: '資料量大（共 500 筆），略過預覽' });

    expect(el(f).querySelector('iframe')).toBeNull();
    expect(text(f)).toContain('資料量大（共 500 筆），略過預覽');
    expect(el(f).querySelector('.print-dialog')!.classList).toContain('no-preview');
  });

  it('preview-only（瀏覽器）：不顯示印表機 / 份數，主鈕文案說實話', async () => {
    const f = await open({ mode: 'preview-only', printers: [], previewUrl: 'blob:abc' });

    expect(el(f).querySelector('select')).toBeNull();
    expect(el(f).querySelector('input[type="number"]')).toBeNull();
    expect(text(f)).not.toContain('印表機');
    // 紙張仍要顯示（那是報表規格）
    expect(text(f)).toContain('21 × 14.8 cm');

    const submit = el(f).querySelector<HTMLButtonElement>('.btn-primary')!;
    expect(submit.textContent!.trim()).toBe('在新分頁開啟');
    expect(submit.disabled).toBe(false); // 沒有印表機也能按
  });

  it('printer 模式下找不到印表機 → 主鈕 disabled', async () => {
    const f = await open({ printers: [] });
    expect(el(f).querySelector<HTMLButtonElement>('.btn-primary')!.disabled).toBe(true);
    expect(text(f)).toContain('找不到可用的印表機');
  });

  it('preview-only 送出的 remember 一律 false（沒有印表機設定可記）', async () => {
    const f = await open({ mode: 'preview-only', printers: [] });
    let result: PrintDialogResult | undefined;
    f.componentInstance.print.subscribe((r) => (result = r));

    el(f).querySelector<HTMLButtonElement>('.btn-primary')!.click();

    expect(result).toEqual({
      deviceName: undefined,
      copies: 1,
      scale: 'driver',
      orientation: 'driver',
      paper: 'driver',
      remember: false,
    });
  });

  it('三個列印方式下拉預設都是「印表機預設」（＝什麼都不指定，改版前的基準）', async () => {
    const f = await open();
    const values = [...el(f).querySelectorAll<HTMLSelectElement>('select')]
      .slice(1) // 第一個是印表機下拉
      .map((s) => s.value);

    expect(values).toEqual(['driver', 'driver', 'driver']);
  });

  it('preview-only 不顯示列印方式三選單（瀏覽器沒有印表機能力）', async () => {
    const f = await open({ mode: 'preview-only', printers: [] });

    expect(el(f).querySelectorAll('select')).toHaveLength(0);
  });

  it('份數超出範圍會被夾回 1–99（送進驅動的值不能是垃圾）', async () => {
    const f = await open({ copies: 1 });
    let result: PrintDialogResult | undefined;
    f.componentInstance.print.subscribe((r) => (result = r));

    const input = el(f).querySelector<HTMLInputElement>('input[type="number"]')!;
    input.value = '250';
    input.dispatchEvent(new Event('input'));
    f.detectChanges();

    el(f).querySelector<HTMLButtonElement>('.btn-primary')!.click();
    expect(result!.copies).toBe(99);
  });

  it('預設帶回上次記住的印表機', async () => {
    const f = await open({
      printers: [
        { name: 'HP-1', displayName: 'HP', isDefault: true, status: 0 },
        { name: 'EPSON-2', displayName: 'EPSON', isDefault: false, status: 0 },
      ],
      deviceName: 'EPSON-2',
    });
    let result: PrintDialogResult | undefined;
    f.componentInstance.print.subscribe((r) => (result = r));

    el(f).querySelector<HTMLButtonElement>('.btn-primary')!.click();
    expect(result!.deviceName).toBe('EPSON-2');
  });
});
