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
    onSameMailAddressChange(): Promise<void>;
    errorMessage: () => string | null;
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
  const signupRow = (
    id: string, believerId: string, name: string, extra?: Partial<SignupListItem>,
  ): SignupListItem => ({
    id, year: 113, ceremonyCategoryId: 'c1', ceremonyTitle: null, signupType: 1,
    numberTitle: null, number: null, fee: null, employee: null, employeeType: 1,
    believerId, name, hallName: null, phone: `0900-${id}`, isFixedNumber: false,
    livingNames: [`${name}陽上`], deadNames: [`${name}往生`],
    mailCity: null, mailZone: null, mailZipcode: null, mailAddress: null,
    textCity: null, textZone: null, textZipcode: null, textAddress: null,
    prepayYear: null, prepayCeremonyCategoryId: null, prepayCeremonyTitle: null,
    remark: `${name}的備註`, adminName: null, createDate: null,
    ...extra,
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
    TestBed.inject(SignupDraftState).draft.set(null); // 草稿是 root singleton，逐案重置
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

  it('開了沒改就離開：內容不會被洗掉（快照＝剛還原回來的同一份）', async () => {
    const draftState = TestBed.inject(SignupDraftState);

    const first = await open();
    probe(first).form.patchValue({ name: '李小華' });
    probe(first).form.markAsDirty();
    first.destroy();

    const second = await open();
    second.destroy(); // 開了沒改就離開

    expect(draftState.draft()?.value.name).toBe('李小華');

    const third = await open();
    expect(val(third, 'name')).toBe('李小華');
  });

  it('按「取消」後切走再回來＝取消後的畫面（法會資料＋費用留著，其餘空）', async () => {
    const first = await open();
    probe(first).form.patchValue({ year: 113, ceremonyCategoryId: 'c1', name: '陳小美', fee: 900 });
    probe(first).form.markAsDirty();

    probe(first).resetBelow(); // 清成新的一筆（此後表單為 pristine）
    first.destroy();           // ＝切到其他功能頁

    const second = await open();
    expect(val(second, 'name')).toBe('');   // 取消清掉的不會復活
    expect(val(second, 'fee')).toBe(900);   // 取消保留的要跟著回來
    expect(val(second, 'year')).toBe(113);
    expect(val(second, 'ceremonyCategoryId')).toBe('c1');
  });

  it('按「取消」保留法會資料與費用，其餘欄位清空（2026-07-28 使用者指定：金額不用重打）', async () => {
    const f = await open();
    probe(f).form.patchValue({
      year: 113, ceremonyCategoryId: 'c1', signupType: 2,
      name: '陳小美', phone: '0900-000-000', fee: 1200, remark: '測試備註',
    });
    probe(f).form.markAsDirty();

    probe(f).resetBelow();

    expect(val(f, 'fee')).toBe(1200);              // 費用保留
    expect(val(f, 'year')).toBe(113);              // 法會資料保留
    expect(val(f, 'ceremonyCategoryId')).toBe('c1');
    expect(val(f, 'signupType')).toBe(2);
    expect(val(f, 'name')).toBe('');               // 信眾以下其餘欄位清空
    expect(val(f, 'phone')).toBe('');
    expect(val(f, 'remark')).toBe('');
  });

  it('只選了信眾、一個字都沒改就切走 → 回來仍帶回（選信眾＝實質輸入）', async () => {
    const draftState = TestBed.inject(SignupDraftState);

    const first = await open();
    const pick = probe(first).pickBeliever(signupRow('s1', 'b1', '林大德'));
    httpMock.expectOne((r) => r.url.endsWith('/believers/b1')).flush(believerStub('b1', '林大德'));
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
    await pick;

    expect(val(f, 'fee')).toBe(1200);            // 使用者打的金額留著
    expect(val(f, 'remark')).toBe('林大德的備註'); // 有資料源的欄位照舊覆蓋
  });

  // 2026-07-31 客訴：勾了「指定編號」再點另一筆信眾就被取消勾選、打好的號碼也不見。
  // 與費用同一取捨，也對齊舊 BelieverSelected（完全沒碰 cbKeepNumber/txtNumber）。
  it('改選信眾不會取消「指定編號」勾選、也不會清掉已輸入的編號', async () => {
    const f = await open();
    probe(f).form.patchValue({ keepNumber: true });
    probe(f).form.patchValue({ customNumber: 128 });

    const pick = probe(f).pickBeliever(signupRow('s1', 'b1', '林大德'));
    httpMock.expectOne((r) => r.url.endsWith('/believers/b1')).flush(believerStub('b1', '林大德'));
    await pick;

    expect(val(f, 'keepNumber')).toBe(true);
    expect(val(f, 'customNumber')).toBe(128);
    expect(probe(f).form.get('customNumber')!.enabled).toBe(true); // 勾著就仍可編輯

    // 按「取消」＝清成新的一筆時仍要清掉（對齊舊 PanelFormEmpty）
    probe(f).resetBelow();
    expect(val(f, 'keepNumber')).toBe(false);
    expect(val(f, 'customNumber')).toBeNull();
  });

  // 2026-07-31 客訴：地址只選了城市與區域（地址欄留空，地址自 2026-07-21 起非必填），
  // 也要能用「同寄件地址」同步文牒段。刻意偏離舊系統的「必須先輸入寄件地址」。
  it('同寄件地址：只選了城市與區域也能同步（地址欄留空）', async () => {
    const f = await open();
    probe(f).form.patchValue({ mailCity: '臺中市', mailZipcodeId: '400', mailAddress: '' });
    probe(f).form.get('sameMailAddress')!.setValue(true);

    const syncing = probe(f).onSameMailAddressChange();
    httpMock.expectOne((r) => r.url.endsWith('/zipcodes') && r.params.get('city') === '臺中市')
      .flush({ items: [{ zipcodeId: 400, area: '中區', zipcode: '400' }] });
    await syncing;

    expect(val(f, 'sameMailAddress')).toBe(true);   // 不再被彈回
    expect(val(f, 'textCity')).toBe('臺中市');
    expect(val(f, 'textZipcodeId')).toBe('400');
    expect(val(f, 'textAddress')).toBe('');
    expect(probe(f).errorMessage()).toBeNull();
  });

  it('同寄件地址：城市／區域／地址全空才擋下並彈回勾選', async () => {
    const f = await open();
    probe(f).form.get('sameMailAddress')!.setValue(true);

    await probe(f).onSameMailAddressChange();

    expect(val(f, 'sameMailAddress')).toBe(false);
    expect(probe(f).errorMessage()).toBe('請先填寫寄件地址（城市／區域或地址）');
  });

  it('連續改選兩位信眾：先選的慢回應不會蓋掉後選的', async () => {
    const f = await open();

    const pickA = probe(f).pickBeliever(signupRow('s1', 'bA', '甲信眾', { prepayYear: 112 }));
    const reqA = httpMock.expectOne((r) => r.url.endsWith('/believers/bA'));
    // 還沒回就改點別列
    const pickB = probe(f).pickBeliever(
      signupRow('s2', 'bB', '乙信眾', { prepayYear: 113, prepayCeremonyCategoryId: 'c9' }),
    );
    const reqB = httpMock.expectOne((r) => r.url.endsWith('/believers/bB'));

    reqB.flush(believerStub('bB', '乙信眾'));
    await flushMicrotasks();

    reqA.flush(believerStub('bA', '甲信眾')); // 甲的慢回應姍姍來遲
    await Promise.all([pickA, pickB]);

    expect(val(f, 'name')).toBe('乙信眾');
    expect(val(f, 'believerId')).toBe('bB');
    expect(val(f, 'remark')).toBe('乙信眾的備註');
    expect((val(f, 'livingNames') as string[])[0]).toBe('乙信眾陽上');
    expect(val(f, 'prepayYear')).toBe(113); // 甲不會把乙的預繳蓋掉
    expect(val(f, 'prepayCeremonyCategoryId')).toBe('c9');
  });

  // 2026-07-31 客訴：同一次搜尋裡先點有預繳的法會列、再點沒預繳的普桌列（SignupType 4），
  // 普桌卻沿用了法會的預繳。法會與普桌是分開報名的兩件事，預繳不互通 → 預繳一律取該列自身值。
  it('改選：法會列的預繳不會殘留到普桌列', async () => {
    const f = await open();

    const pickCeremony = probe(f).pickBeliever(
      signupRow('s1', 'b1', '林大德', { signupType: 1, prepayYear: 121, prepayCeremonyCategoryId: 'c9' }),
    );
    httpMock.expectOne((r) => r.url.endsWith('/believers/b1')).flush(believerStub('b1', '林大德'));
    await pickCeremony;

    expect(val(f, 'prepayYear')).toBe(121);
    expect(val(f, 'prepayCeremonyCategoryId')).toBe('c9');

    // 同一位信眾的普桌報名（該筆沒有預繳）
    const pickWorship = probe(f).pickBeliever(
      signupRow('s2', 'b1', '林大德', { signupType: 4, prepayYear: null, prepayCeremonyCategoryId: null }),
    );
    httpMock.expectOne((r) => r.url.endsWith('/believers/b1')).flush(believerStub('b1', '林大德'));
    await pickWorship;

    expect(val(f, 'prepayYear')).toBeNull();
    expect(val(f, 'prepayCeremonyCategoryId')).toBe('');
    // 不再有「查信眾最新一筆預繳」的跨類型查詢（那正是把法會預繳帶到普桌的來源）
    httpMock.expectNone((r) => r.url.includes('/prepay'));
  });

  it('新增成功：表單資料留著、跳「編號X，新增報名成功」，切走再回來仍是同一份', async () => {
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
    expect(dialogCalls.at(-1)?.message).toBe('編號123，新增報名成功');
    expect(dialogCalls.at(-1)?.hideCancel).toBe(true);

    // 存檔後表單是 pristine，但畫面上有東西 → 一律快照，回來要跟離開前一樣（2026-07-28）
    f.destroy();
    const again = await open();
    expect(val(again, 'name')).toBe('王小明');
    expect(val(again, 'remark')).toBe('第一筆');
    expect(probe(again).lastCreatedSignupId()).toBe('s9'); // 「列印資料卡」維持可按
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
