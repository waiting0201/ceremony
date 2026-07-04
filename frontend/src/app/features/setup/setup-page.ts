import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ceremony, DbConfigInput } from '../../core/platform/electron';

// 首次啟動 DB 連線設定頁（Electron-only）：填主機/port/名稱/帳密 → 測試連線 → 儲存並連線。
// 連線成功後由 main process 帶 apiBase 重新載入 renderer（不在此導航）。
// 已有設定時（重新連線情境）提供「用既有設定連線」直接重連（main 記憶體內含密碼）。
@Component({
  selector: 'app-setup-page',
  imports: [ReactiveFormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="wrap">
      <div class="card">
        <h1>資料庫連線設定</h1>
        <p class="lead">
          首次使用請填入寺方資料庫主機資訊。設定會儲存於本機
          <code>%APPDATA%\\Ceremony\\config.json</code>。
        </p>

        @if (hasConfig()) {
          <div class="existing">
            <span>偵測到既有設定（{{ existingSummary() }}）。</span>
            <button type="button" class="btn btn-sm" [disabled]="busy()" (click)="reconnect()">
              用既有設定連線
            </button>
          </div>
        }

        <form [formGroup]="form" (ngSubmit)="save()">
          <div class="grid">
            <label class="field span2">
              <span>資料庫主機 (IP 或主機名)</span>
              <input type="text" formControlName="dbHost" placeholder="192.168.1.151" autocomplete="off" />
            </label>
            <label class="field">
              <span>連接埠</span>
              <input type="number" formControlName="dbPort" placeholder="1433" />
            </label>
            <label class="field">
              <span>資料庫名稱</span>
              <input type="text" formControlName="dbName" placeholder="Ceremony" autocomplete="off" />
            </label>
            <label class="field">
              <span>帳號</span>
              <input type="text" formControlName="dbUser" placeholder="sa" autocomplete="off" />
            </label>
            <label class="field">
              <span>密碼</span>
              <input type="password" formControlName="dbPassword" autocomplete="off" />
            </label>
          </div>

          <div class="actions">
            <button type="button" class="btn" [disabled]="form.invalid || busy()" (click)="test()">
              {{ testing() ? '測試中…' : '測試連線' }}
            </button>
            <button type="submit" class="btn btn-primary" [disabled]="form.invalid || busy()">
              {{ saving() ? '連線中…' : '儲存並連線' }}
            </button>
          </div>

          @if (message(); as m) {
            <p class="msg" [class.error]="isError()">{{ m }}</p>
          }
        </form>
      </div>
    </div>
  `,
  styles: `
    .wrap { min-height: 100vh; display: flex; align-items: center; justify-content: center; padding: 24px; background: var(--c-bg); }
    .card { background: var(--c-surface); border: 1px solid var(--c-border); border-radius: 8px; padding: 28px 32px; max-width: 600px; width: 100%; }
    h1 { font-size: var(--font-size-xl); margin: 0 0 8px; }
    .lead { margin: 0 0 16px; color: var(--c-text-secondary); line-height: 1.6; }
    code { background: var(--c-bg); padding: 1px 5px; border-radius: 3px; font-family: var(--font-mono, monospace); font-size: var(--font-size-xs); }
    .existing { display: flex; align-items: center; justify-content: space-between; gap: 12px; background: var(--c-bg); border: 1px solid var(--c-border-soft); border-radius: 6px; padding: 10px 12px; margin-bottom: 16px; font-size: var(--font-size-base); }
    .grid { display: grid; grid-template-columns: 1fr 1fr; gap: 14px; margin-bottom: 18px; }
    .field { display: flex; flex-direction: column; gap: 6px; font-size: var(--font-size-base); }
    .field.span2 { grid-column: 1 / -1; }
    .field span { color: var(--c-text-secondary); }
    input { border: 1px solid var(--c-border); border-radius: 5px; padding: 8px 10px; font-size: var(--font-size-base); background: var(--c-surface); color: var(--c-text-primary); }
    input:focus { outline: none; border-color: var(--c-primary); }
    .actions { display: flex; gap: 10px; }
    .msg { margin: 14px 0 0; font-size: var(--font-size-base); color: var(--c-success); }
    .msg.error { color: var(--c-danger); }
  `,
})
export class SetupPage {
  private readonly fb = inject(FormBuilder);
  private readonly bridge = ceremony();

  protected readonly hasConfig = signal(false);
  protected readonly existingSummary = signal('');
  protected readonly testing = signal(false);
  protected readonly saving = signal(false);
  protected readonly message = signal<string | null>(null);
  protected readonly isError = signal(false);

  protected readonly form = this.fb.nonNullable.group({
    dbHost: ['', [Validators.required]],
    dbPort: [1433, [Validators.required, Validators.min(1), Validators.max(65535)]],
    dbName: ['Ceremony', [Validators.required]],
    dbUser: ['', [Validators.required]],
    dbPassword: ['', [Validators.required]],
  });

  constructor() {
    void this.prefill();
  }

  protected busy(): boolean {
    return this.testing() || this.saving();
  }

  private async prefill(): Promise<void> {
    if (!this.bridge) return;
    const status = await this.bridge.getStatus();
    if (status.config) {
      this.hasConfig.set(true);
      this.existingSummary.set(
        `${status.config.dbHost},${status.config.dbPort} / ${status.config.dbName}`,
      );
      this.form.patchValue({
        dbHost: status.config.dbHost,
        dbPort: status.config.dbPort,
        dbName: status.config.dbName,
        dbUser: status.config.dbUser,
      });
    }
    if (!status.connected && status.hasConfig) {
      this.setMessage('無法以既有設定連線，請確認資訊或重新填寫。', true);
    }
  }

  private payload(): DbConfigInput {
    return this.form.getRawValue();
  }

  private setMessage(msg: string, error: boolean): void {
    this.message.set(msg);
    this.isError.set(error);
  }

  protected async test(): Promise<void> {
    if (!this.bridge || this.form.invalid) return;
    this.testing.set(true);
    this.message.set(null);
    try {
      const r = await this.bridge.testConnection(this.payload());
      this.setMessage(r.ok ? '連線成功！' : `連線失敗：${r.error ?? '未知錯誤'}`, !r.ok);
    } finally {
      this.testing.set(false);
    }
  }

  protected async save(): Promise<void> {
    if (!this.bridge || this.form.invalid) return;
    this.saving.set(true);
    this.message.set(null);
    try {
      const r = await this.bridge.saveConfigAndConnect(this.payload());
      // 成功時 main 會帶 apiBase 重新載入 renderer，本元件隨之卸載。
      if (!r.ok) this.setMessage(`連線失敗：${r.error ?? '未知錯誤'}`, true);
    } finally {
      this.saving.set(false);
    }
  }

  protected async reconnect(): Promise<void> {
    if (!this.bridge) return;
    this.saving.set(true);
    this.message.set(null);
    try {
      const r = await this.bridge.connect();
      if (!r.ok) this.setMessage(`連線失敗：${r.error ?? '未知錯誤'}`, true);
    } finally {
      this.saving.set(false);
    }
  }
}
