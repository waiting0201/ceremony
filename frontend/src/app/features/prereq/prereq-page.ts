import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { ceremony, PrereqItem, PrereqReport } from '../../core/platform/electron';

// 首次啟動軟體偵測頁（Electron-only）：列出 VC++ Redistributable / .NET 10 ASP.NET Core Runtime
// 安裝狀態，缺項提供「安裝 / 前往下載」，全部就緒後「重新檢查」→ 進入 /setup。
@Component({
  selector: 'app-prereq-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="wrap">
      <div class="card">
        <h1>系統環境檢查</h1>
        <p class="lead">
          啟動前需確認本機已安裝下列必要元件。缺少時請點「安裝」或「前往下載」，安裝完成後按「重新檢查」。
        </p>

        @if (report(); as r) {
          <ul class="items">
            @for (item of r.items; track item.key) {
              <li class="item" [class.ok]="item.ok" [class.bad]="!item.ok">
                <span class="status">{{ item.ok ? '✓' : '✕' }}</span>
                <div class="info">
                  <div class="name">{{ item.name }}</div>
                  @if (item.detail) {
                    <div class="detail">{{ item.detail }}</div>
                  }
                </div>
                @if (!item.ok) {
                  <div class="actions">
                    <button type="button" class="btn btn-primary btn-sm" (click)="install(item)">安裝</button>
                    <button type="button" class="btn btn-sm" (click)="openLink(item)">前往下載</button>
                  </div>
                }
              </li>
            }
          </ul>

          @if (r.skipped) {
            <p class="hint">目前非 Windows 環境（開發模式），略過實際偵測。</p>
          }
        } @else {
          <p class="hint">偵測中…</p>
        }

        <div class="footer">
          <button type="button" class="btn btn-primary" [disabled]="checking()" (click)="recheck()">
            {{ checking() ? '檢查中…' : '重新檢查' }}
          </button>
          @if (message()) {
            <span class="msg">{{ message() }}</span>
          }
        </div>
      </div>
    </div>
  `,
  styles: `
    .wrap { min-height: 100vh; display: flex; align-items: center; justify-content: center; padding: 24px; background: var(--c-bg); }
    .card { background: var(--c-surface); border: 1px solid var(--c-border); border-radius: 8px; padding: 28px 32px; max-width: 640px; width: 100%; }
    h1 { font-size: 22px; margin: 0 0 8px; }
    .lead { margin: 0 0 20px; color: var(--c-text-secondary); line-height: 1.6; }
    .items { list-style: none; margin: 0 0 16px; padding: 0; display: flex; flex-direction: column; gap: 10px; }
    .item { display: flex; align-items: center; gap: 12px; border: 1px solid var(--c-border-soft); border-radius: 6px; padding: 12px 14px; }
    .item.ok { border-color: var(--c-success); }
    .item.bad { border-color: var(--c-danger); }
    .status { font-size: 18px; font-weight: 700; width: 20px; text-align: center; }
    .item.ok .status { color: var(--c-success); }
    .item.bad .status { color: var(--c-danger); }
    .info { flex: 1; }
    .name { font-weight: 600; }
    .detail { font-size: 13px; color: var(--c-text-secondary); margin-top: 2px; }
    .actions { display: flex; gap: 8px; }
    .footer { display: flex; align-items: center; gap: 12px; margin-top: 8px; }
    .msg { color: var(--c-text-secondary); font-size: 14px; }
    .hint { color: var(--c-text-secondary); font-size: 13px; margin: 0 0 12px; }
  `,
})
export class PrereqPage {
  private readonly router = inject(Router);
  private readonly bridge = ceremony();

  protected readonly report = signal<PrereqReport | null>(null);
  protected readonly checking = signal(false);
  protected readonly message = signal<string | null>(null);

  constructor() {
    void this.load();
  }

  private async load(): Promise<void> {
    if (!this.bridge) return;
    const status = await this.bridge.getStatus();
    this.report.set(status.prereqs);
  }

  protected async recheck(): Promise<void> {
    if (!this.bridge) return;
    this.checking.set(true);
    this.message.set(null);
    try {
      const r = await this.bridge.recheckPrereqs();
      this.report.set(r);
      if (r.ok) {
        this.message.set('環境就緒，前往連線設定…');
        await this.router.navigateByUrl('/setup');
      } else {
        this.message.set('仍有元件未安裝，請安裝後再檢查。');
      }
    } finally {
      this.checking.set(false);
    }
  }

  protected async install(item: PrereqItem): Promise<void> {
    if (!this.bridge) return;
    const r = await this.bridge.launchInstaller(item.key);
    this.message.set(
      r.launched ? '安裝程式已啟動，完成後請按「重新檢查」。' : '已開啟下載頁，安裝完成後請按「重新檢查」。',
    );
  }

  protected async openLink(item: PrereqItem): Promise<void> {
    await this.bridge?.openExternal(item.downloadUrl);
  }
}
