import { ApplicationRef } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { PrintDialogService } from './print-dialog.service';
import type { PrintDialogRequest } from './print-dialog.types';

/**
 * 防 leak 的行為鎖：預覽的 object URL 若沒成對 revoke，整份 PDF 會留在記憶體裡，
 * 而批次列印一次可能是數十 MB——連印一天就吃光。
 */
describe('PrintDialogService', () => {
  let created: string[];
  let revoked: string[];
  let origCreate: typeof URL.createObjectURL;
  let origRevoke: typeof URL.revokeObjectURL;
  let sut: PrintDialogService;

  const request = (patch: Partial<PrintDialogRequest> = {}): PrintDialogRequest => ({
    reportLabel: '資料卡',
    paperLabel: '21 × 14.8 cm',
    printers: [{ name: 'HP-1', displayName: 'HP', isDefault: true, status: 0 }],
    copies: 1,
    scaleMode: 'actual',
    mode: 'printer',
    ...patch,
  });

  beforeEach(() => {
    created = [];
    revoked = [];
    origCreate = URL.createObjectURL;
    origRevoke = URL.revokeObjectURL;
    let n = 0;
    URL.createObjectURL = () => {
      const url = `blob:fake-${n++}`;
      created.push(url);
      return url;
    };
    URL.revokeObjectURL = (url: string) => revoked.push(url);

    TestBed.configureTestingModule({});
    sut = TestBed.inject(PrintDialogService);
  });

  afterEach(() => {
    URL.createObjectURL = origCreate;
    URL.revokeObjectURL = origRevoke;
  });

  /** 對話框是 CDK Overlay 動態建的；插值文字要等一次變更偵測才會出現。 */
  async function settle(): Promise<void> {
    await new Promise((r) => setTimeout(r, 0));
    TestBed.inject(ApplicationRef).tick();
  }

  const button = (selector: '.btn-primary' | '.btn'): HTMLButtonElement => {
    const found = document.querySelector<HTMLButtonElement>(`.print-actions ${selector}`);
    if (!found) throw new Error(`找不到按鈕 ${selector}`);
    return found;
  };
  const confirm = () => button('.btn-primary');
  const cancel = () => button('.btn');

  it('按列印 → 回傳選擇，並回收預覽 URL', async () => {
    const p = sut.ask(request({ previewBlob: new Blob(['%PDF-']) }));
    await settle();

    expect(created).toEqual(['blob:fake-0']);
    expect(confirm().textContent!.trim()).toBe('列印');
    confirm().click();

    await expect(p).resolves.toMatchObject({ copies: 1, scaleMode: 'actual' });
    expect(revoked).toEqual(['blob:fake-0']);
  });

  it('按取消 → 回 null，一樣要回收（取消才是最常走的那條路）', async () => {
    const p = sut.ask(request({ previewBlob: new Blob(['%PDF-']) }));
    await settle();

    cancel().click();

    await expect(p).resolves.toBeNull();
    expect(revoked).toEqual(['blob:fake-0']);
  });

  it('沒給 previewBlob 就不建 URL（大檔略過預覽的路徑不該平白 leak）', async () => {
    const p = sut.ask(request({ previewNotice: '資料量大，略過預覽' }));
    await settle();

    expect(created).toEqual([]);
    cancel().click();

    await p;
    expect(revoked).toEqual([]);
  });

  it('對話框關閉後 overlay 不留在 DOM', async () => {
    const p = sut.ask(request());
    await settle();
    expect(document.querySelector('.print-dialog')).not.toBeNull();

    cancel().click();
    await p;

    expect(document.querySelector('.print-dialog')).toBeNull();
  });
});
