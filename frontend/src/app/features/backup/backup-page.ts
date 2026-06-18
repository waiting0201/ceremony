import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { BackupApi } from '../../core/api/backup/backup.api';
import { ApiError } from '../../core/http/api-error';
import { ConfirmDialogService } from '../../shared/confirm-dialog/confirm-dialog.service';
import { IconComponent } from '../../shared/icon/icon.component';

@Component({
  selector: 'app-backup-page',
  imports: [IconComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="page">
      <h1>資料備份</h1>
      <div class="card">
        <p class="lead">
          將目前的資料庫完整備份成 <code>.bak</code> 檔（沿用舊系統
          <code>BACKUP DATABASE</code> 流程）。備份檔由伺服器寫入後台設定的備份目錄（<code>D:\\Backup</code>）。
        </p>
        <p class="hint">備份期間請勿關閉視窗；完成後會顯示檔案路徑與大小。</p>
        <label class="clearlog">
          <input
            type="checkbox"
            [checked]="clearLog()"
            [disabled]="running()"
            (change)="clearLog.set($any($event.target).checked)"
          />
          <span>備份後清除交易紀錄檔（transaction log）</span>
        </label>
        <p class="hint">勾選後會截斷並收縮交易紀錄檔以釋放空間（DBA 級操作）；備份本身不受影響。</p>
        <button type="button" class="btn btn-primary" [disabled]="running()" (click)="onBackup()">
          <app-icon name="database" [size]="18" />
          {{ running() ? '備份中…' : '立即備份' }}
        </button>
      </div>
    </div>
  `,
  styles: `
    .page { padding: 4px 0; }
    h1 { font-size: var(--font-size-xl, 22px); margin: 0 0 var(--space-lg, 16px); }
    .card {
      background: var(--c-surface);
      border: 1px solid var(--c-border);
      border-radius: 6px;
      padding: var(--space-lg, 20px);
      max-width: 640px;
    }
    .lead { margin: 0 0 8px; line-height: 1.6; }
    .hint { margin: 0 0 var(--space-lg, 16px); color: var(--c-text-secondary, #777); font-size: 13px; }
    code {
      background: var(--c-bg, #f4f1e9);
      padding: 1px 5px;
      border-radius: 3px;
      font-family: var(--font-mono, monospace);
    }
    .btn-primary { display: inline-flex; align-items: center; gap: 6px; }
    .clearlog {
      display: flex;
      align-items: center;
      gap: 8px;
      margin: 0 0 4px;
      cursor: pointer;
      font-size: 14px;
      input { cursor: pointer; }
    }
  `,
})
export class BackupPage {
  private readonly api = inject(BackupApi);
  private readonly dialog = inject(ConfirmDialogService);

  protected readonly running = signal(false);
  protected readonly clearLog = signal(false);

  protected async onBackup(): Promise<void> {
    if (this.running()) return;

    const willClearLog = this.clearLog();
    const confirmed = await this.dialog.ask({
      title: '資料備份',
      message: willClearLog
        ? '確定要執行資料庫備份嗎？\n\n⚠ 已勾選「清除交易紀錄檔」：備份完成後會截斷並收縮 transaction log。'
        : '確定要執行資料庫備份嗎？',
      confirmLabel: '開始備份',
      danger: willClearLog,
    });
    if (!confirmed) return;

    this.running.set(true);
    try {
      const r = await this.api.run({ clearLog: willClearLog });
      const lines = [
        `檔案：${r.fileName}`,
        `路徑：${r.fullPath}`,
        `大小：${formatBytes(r.sizeBytes)}`,
      ];
      if (willClearLog) {
        if (r.logCleared) {
          lines.push(
            r.logBackupFileName
              ? `交易紀錄檔已清除（log backup：${r.logBackupFileName}）`
              : '交易紀錄檔已清除',
          );
        } else {
          lines.push(`⚠ 備份成功，但清除交易紀錄檔失敗：${r.logClearError ?? '未知原因'}`);
        }
      }
      await this.dialog.ask({
        title: '備份完成！',
        message: lines.join('\n'),
        confirmLabel: '確定',
        hideCancel: true,
      });
    } catch (err) {
      const message = err instanceof ApiError ? err.message : '備份失敗，請稍後再試';
      await this.dialog.ask({
        title: '備份失敗',
        message,
        confirmLabel: '關閉',
        hideCancel: true,
        danger: true,
      });
    } finally {
      this.running.set(false);
    }
  }
}

function formatBytes(bytes: number): string {
  if (!bytes) return '0 B';
  const mb = bytes / (1024 * 1024);
  if (mb >= 1) return `${mb.toFixed(1)} MB`;
  return `${(bytes / 1024).toFixed(1)} KB`;
}
