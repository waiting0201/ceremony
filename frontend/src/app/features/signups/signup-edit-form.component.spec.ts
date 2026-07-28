import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import type { FormGroup } from '@angular/forms';
import type { BelieverListItem } from '../../core/api/believers/believer.models';
import type { SignupListItem } from '../../core/api/signups/signup.models';
import { ConfirmDialogService } from '../../shared/confirm-dialog/confirm-dialog.service';
import type { ConfirmDialogConfig } from '../../shared/confirm-dialog/confirm-dialog.types';
import { SignupEditFormComponent, type SignupSavedEvent } from './signup-edit-form.component';
import { SignupDraftState } from './signup-draft-state';

/**
 * 「新增報名填到一半，切到其他功能頁再回來，資料不能被清掉」的回歸測試（2026-07-27 客訴）。
 * 元件銷毀＝離開表單（關 overlay / 換路由），重新 createComponent＝再次開啟新增報名。
 */
describe('SignupEditFormComponent（草稿保留 / 改選信眾 / 編號欄啟用）', () => {
  /** form / pickBeliever 是 protected，測試用最小可用型別存取。 */
  type Probe = {
    form: FormGroup;
    resetBelow(): void;
    submit(): Promise<void>;
    pickBeliever(row: SignupListItem): Promise<void>;
    lastCreatedSignupId: () => string | null;
    printDataCard(): Promise<void>;
  };

  const probe = (f: ComponentFixture<SignupEditFormComponent>): Probe =>
    f.componentInstance as unknown as Probe;

  const val = (f: ComponentFixture<SignupEditFormComponent>, path: string): unknown =>
    probe(f).form.get(path)!.value;

  async function open(
    inputs?: Record<string, unknown>,
  ): Promise<ComponentFixture<SignupEditFormComponent>> {
    const f = TestBed.createComponent(SignupEditFormComponent);
    for (const [k, v] of Object.entries(inputs ?? {})) f.componentRef.setInput(k, v);
    f.detectChanges();
    await f.whenStable();
    return f;
  }

  /** 一列搜尋結果（＝一筆報名）；地址留空以免測試還要 flush 區域清單。 */
  const signupRow = (id: string, believerId: string, name: string): SignupListItem => ({
    id, year: 113, ceremonyCategoryId: 'c1', ceremonyTitle: null, signupType: 1,
    numberTitle: null, number: null, fee: null, employee: null, employeeType: 1,
    believerId, name, hallName: null, phone: `0900-${id}`, isFixedNumber: false,
    livingNames: [`${name}陽上`], deadNames: [`${name}往生`],
    mailCity: null, mailZone: null, mailZipcode: null, mailAddress: null,
    textCity: null, textZone: null, textZipcode: null, textAddress: null,
    prepayYear: null, prepayCeremonyCategoryId: null, prepayCeremonyTitle: null,
    remark: `${name}的備註`, adminName: null, createDate: null,
  });

  const believerStub = (id: string, name: string): BelieverListItem => ({
    id, employeeType: 1, employeeTypeTitle: '非員工', hallName: null, name,
    phone: null, isFixedNumber: false,
    mailZipcodeId: null, mailCity: null, mailArea: null, mailAddress: null,
    textZipcodeId: null, textCity: null, textArea: null, textAddress: null,
    livingNames: [], deadNames: [],
  });

  /** 讓 pending 的 promise chain 往前推進（每個 await 一輪）。 */
  const flushMicrotasks = () => new Promise<void>((r) => setTimeout(r));

  let httpMock: HttpTestingController;
  /** 攔下成功/確認 dialog 的呼叫（真的 dialog 會等使用者點擊，測試裡會卡住）。 */
  let dialogCalls: ConfirmDialogConfig[];

  beforeEach(() => {
    dialogCalls = [];
    TestBed.configureTestingModule({
      imports: [SignupEditFormComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        {
          provide: ConfirmDialogService,
          useValue: {
            ask: (config: ConfirmDialogConfig) => {
              dialogCalls.push(config);
              return Promise.resolve(true);
            },
          },
        },
      ],
    });
    TestBed.inject(SignupDraftState).clear();
    httpMock = TestBed.inject(HttpTestingController);
  });

  it('純新增模式：離開表單會存草稿，重新開啟自動帶回', async () => {
    const draftState = TestBed.inject(SignupDraftState);

    const first = await open();
    const form = probe(first).form;
    form.patchValue({ name: '王小明', phone: '0912345678', remark: '填到一半', fee: 600 });
    form.get('livingNames')!.setValue(['陽上甲', '', '', '', '', '']);
    form.get('deadNames')!.setValue(['往生乙', '', '', '', '', '']);
    form.markAsDirty();

    first.destroy(); // ＝關掉表單切到其他功能頁
    expect(draftState.draft()?.value.name).toBe('王小明');

    const second = await open(); // ＝再回到新增報名
    expect(val(second, 'name')).toBe('王小明');
    expect(val(second, 'phone')).toBe('0912345678');
    expect(val(second, 'remark')).toBe('填到一半');
    expect(val(second, 'fee')).toBe(600);
    expect((val(second, 'livingNames') as string[])[0]).toBe('陽上甲');
    expect((val(second, 'deadNames') as string[])[0]).toBe('往生乙');
  });

  it('沒動過的空白表單不會覆蓋既有草稿', async () => {
    const draftState = TestBed.inject(SignupDraftState);

    const first = await open();
    probe(first).form.patchValue({ name: '李小華' });
    probe(first).form.markAsDirty();
    first.destroy();

    const second = await open();
    second.destroy(); // 開了沒改就離開

    expect(draftState.draft()?.value.name).toBe('李小華');
  });

  it('按「取消」（清成新的一筆）會把草稿一併作廢', async () => {
    const draftState = TestBed.inject(SignupDraftState);

    const first = await open();
    probe(first).form.patchValue({ name: '陳小美' });
    probe(first).form.markAsDirty();
    first.destroy();
    expect(draftState.draft()).not.toBeNull();

    const second = await open();
    expect(val(second, 'name')).toBe('陳小美');
    probe(second).resetBelow();
    expect(draftState.draft()).toBeNull();
    expect(val(second, 'name')).toBe('');

    second.destroy();
    expect(draftState.draft()).toBeNull(); // resetBelow 後表單已 pristine，不會再存回去
  });

  it('只選了信眾、一個字都沒改就切走 → 回來仍帶回（選信眾＝實質輸入）', async () => {
    const draftState = TestBed.inject(SignupDraftState);

    const first = await open();
    const pick = probe(first).pickBeliever(signupRow('s1', 'b1', '林大德'));
    httpMock.expectOne((r) => r.url.endsWith('/believers/b1')).flush(believerStub('b1', '林大德'));
    await flushMicrotasks();
    httpMock.expectOne((r) => r.url.includes('/prepay'))
      .flush({ prepayYear: null, prepayCeremonyCategoryId: null });
    await pick;

    first.destroy();
    expect(draftState.draft()?.value.name).toBe('林大德');

    const second = await open();
    expect(val(second, 'name')).toBe('林大德');
    expect(val(second, 'remark')).toBe('林大德的備註');
    expect((val(second, 'deadNames') as string[])[0]).toBe('林大德往生');
  });

  it('改選信眾不會把使用者輸入的費用清掉（備註/名單等仍照該筆報名覆蓋）', async () => {
    const f = await open();
    probe(f).form.patchValue({ fee: 1200 });

    const pick = probe(f).pickBeliever(signupRow('s1', 'b1', '林大德'));
    httpMock.expectOne((r) => r.url.endsWith('/believers/b1')).flush(believerStub('b1', '林大德'));
    await flushMicrotasks();
    httpMock.expectOne((r) => r.url.includes('/prepay'))
      .flush({ prepayYear: null, prepayCeremonyCategoryId: null });
    await pick;

    expect(val(f, 'fee')).toBe(1200);            // 使用者打的金額留著
    expect(val(f, 'remark')).toBe('林大德的備註'); // 有資料源的欄位照舊覆蓋
  });

  it('連續改選兩位信眾：先選的慢回應不會蓋掉後選的', async () => {
    const f = await open();

    const pickA = probe(f).pickBeliever(signupRow('s1', 'bA', '甲信眾'));
    const reqA = httpMock.expectOne((r) => r.url.endsWith('/believers/bA'));
    const pickB = probe(f).pickBeliever(signupRow('s2', 'bB', '乙信眾')); // 還沒回就改點別列
    const reqB = httpMock.expectOne((r) => r.url.endsWith('/believers/bB'));

    reqB.flush(believerStub('bB', '乙信眾'));
    await flushMicrotasks();
    httpMock.expectOne((r) => r.url.includes('/prepay'))
      .flush({ prepayYear: 113, prepayCeremonyCategoryId: 'c9' });

    reqA.flush(believerStub('bA', '甲信眾')); // 甲的慢回應姍姍來遲
    await Promise.all([pickA, pickB]);

    expect(val(f, 'name')).toBe('乙信眾');
    expect(val(f, 'believerId')).toBe('bB');
    expect(val(f, 'remark')).toBe('乙信眾的備註');
    expect((val(f, 'livingNames') as string[])[0]).toBe('乙信眾陽上');
    expect(val(f, 'prepayYear')).toBe(113); // 甲不會把乙的預繳蓋掉
    httpMock.expectNone((r) => r.url.includes('/prepay')); // 作廢的甲不再往下查預繳
  });

  it('新增成功：表單資料留著、草稿記憶清掉、跳「編號X，新增報名成功」', async () => {
    const draftState = TestBed.inject(SignupDraftState);

    const f = await open();
    probe(f).form.patchValue({ ceremonyCategoryId: 'c1', name: '王小明', remark: '第一筆' });
    probe(f).form.markAsDirty();

    let savedEvent: SignupSavedEvent | undefined;
    f.componentInstance.saved.subscribe((e) => (savedEvent = e));

    const submitting = probe(f).submit();
    // 沒選信眾 → 先自動建信眾，再建報名
    httpMock.expectOne((r) => r.method === 'POST' && r.url.endsWith('/believers'))
      .flush(believerStub('b9', '王小明'));
    await flushMicrotasks();
    httpMock.expectOne((r) => r.method === 'POST' && r.url.endsWith('/signups'))
      .flush({ ...signupRow('s9', 'b9', '王小明'), number: 123 });
    await submitting;

    expect(val(f, 'name')).toBe('王小明');       // 資料不清除
    expect(val(f, 'remark')).toBe('第一筆');
    expect(savedEvent?.keepOpen).toBe(true);      // host 不關閉表單、只重查列表
    expect(draftState.draft()).toBeNull();        // 但草稿記憶要清掉
    expect(dialogCalls.at(-1)?.message).toBe('編號123，新增報名成功');
    expect(dialogCalls.at(-1)?.hideCancel).toBe(true);

    f.destroy(); // 存檔後表單已 pristine → 不會把剛存好的內容又記成草稿
    expect(draftState.draft()).toBeNull();
  });

  it('列印資料卡：存檔前不可按，新增成功後印的是剛新增的那一筆', async () => {
    // jsdom 沒有 createObjectURL / 實作 window.open，先接管掉（本測試只驗「有沒有打對 API」）
    const origCreate = URL.createObjectURL;
    const origRevoke = URL.revokeObjectURL;
    const origOpen = window.open;
    URL.createObjectURL = () => 'blob:test';
    URL.revokeObjectURL = () => undefined;
    window.open = () => null;
    try {
      const f = await open();
      probe(f).form.patchValue({ ceremonyCategoryId: 'c1', name: '王小明' });
      probe(f).form.markAsDirty();
      expect(probe(f).lastCreatedSignupId()).toBeNull(); // 存檔前按鈕 disabled 的依據

      const submitting = probe(f).submit();
      httpMock.expectOne((r) => r.method === 'POST' && r.url.endsWith('/believers'))
        .flush(believerStub('b9', '王小明'));
      await flushMicrotasks();
      httpMock.expectOne((r) => r.method === 'POST' && r.url.endsWith('/signups'))
        .flush({ ...signupRow('s9', 'b9', '王小明'), number: 123 });
      await submitting;

      expect(probe(f).lastCreatedSignupId()).toBe('s9');

      const printing = probe(f).printDataCard();
      const req = httpMock.expectOne((r) => r.url.endsWith('/reports/datacard'));
      expect(req.request.params.get('signupId')).toBe('s9');
      req.flush(new Blob(['%PDF-']));
      await printing;

      probe(f).resetBelow();
      expect(probe(f).lastCreatedSignupId()).toBeNull(); // 「取消」＝清成新的一筆 → 又不可按
    } finally {
      URL.createObjectURL = origCreate;
      URL.revokeObjectURL = origRevoke;
      window.open = origOpen;
    }
  });

  it('編號欄恆顯示：新增模式預設 disabled，勾「指定編號」才 enabled', async () => {
    const f = await open();
    const number = () => probe(f).form.get('customNumber')!;

    expect(number().disabled).toBe(true);          // 沒勾就是唯讀（欄位仍在畫面上）
    probe(f).form.get('keepNumber')!.setValue(true);
    expect(number().enabled).toBe(true);
    probe(f).form.get('keepNumber')!.setValue(false);
    expect(number().disabled).toBe(true);
  });

  // 2026-07-28 使用者指定：「確認」只能用滑鼠點，Enter 不可以送出。
  it('按 Enter 不送出表單（備註 textarea 仍可換行）', async () => {
    const f = await open();
    const host: HTMLElement = f.nativeElement;

    // 隱含送出的來源就是表單內的 submit 按鈕，不能再有
    expect(host.querySelector('button[type="submit"]')).toBeNull();

    const enter = () => new KeyboardEvent('keydown', { key: 'Enter', bubbles: true, cancelable: true });

    const nameInput = host.querySelector<HTMLInputElement>('input[formControlName="name"]')!;
    const onInput = enter();
    nameInput.dispatchEvent(onInput);
    expect(onInput.defaultPrevented).toBe(true);

    const remark = host.querySelector<HTMLTextAreaElement>('textarea[formControlName="remark"]')!;
    const onTextarea = enter();
    remark.dispatchEvent(onTextarea);
    expect(onTextarea.defaultPrevented).toBe(false);
  });

  it('編輯模式編號恆可改（不受「指定編號」影響）', async () => {
    const f = await open({ signupId: 'a1b2c3' });
    expect(probe(f).form.get('customNumber')!.enabled).toBe(true);
  });

  it('編輯既有報名不存草稿（有自己的資料來源）', async () => {
    const draftState = TestBed.inject(SignupDraftState);

    const editing = await open({ signupId: 'a1b2c3' });
    probe(editing).form.patchValue({ name: '編輯中的名字' });
    probe(editing).form.markAsDirty();
    editing.destroy();

    expect(draftState.draft()).toBeNull();
  });
});
