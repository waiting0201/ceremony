import { Component, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTableModule } from '@angular/material/table';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { SignupsService } from './signups.service';
import { AuthService } from '../../core/services/auth.service';

@Component({
  selector: 'app-load-prepay',
  standalone: true,
  imports: [MatButtonModule, MatIconModule, MatTableModule, MatProgressBarModule],
  template: `
    <div class="mx-auto max-w-4xl">
      <div class="mb-4 flex items-center gap-3">
        <button mat-icon-button (click)="router.navigate(['/signups'])">
          <mat-icon class="text-gray-800">arrow_back</mat-icon>
        </button>
        <h2 class="text-xl font-semibold text-gray-800">匯入預付款</h2>
      </div>

      <div class="rounded-lg border border-gray-200 bg-white p-6">
        <div class="mb-4">
          <input type="file" #fileInput accept=".csv,.txt,.xlsx" (change)="onFileSelect($event)"
                 class="hidden" />
          <button mat-flat-button color="primary" (click)="fileInput.click()">
            <mat-icon>upload_file</mat-icon> 選擇檔案
          </button>
          @if (fileName()) {
            <span class="ml-3 text-gray-800">{{ fileName() }}</span>
          }
        </div>

        @if (loading()) {
          <mat-progress-bar mode="indeterminate" />
        }

        @if (previewRows().length > 0) {
          <div class="mb-4 overflow-hidden rounded border border-gray-200">
            <table mat-table [dataSource]="previewRows()" class="!bg-white w-full">
              <ng-container matColumnDef="Name">
                <th mat-header-cell *matHeaderCellDef class="!text-gray-800 !bg-gray-100">姓名</th>
                <td mat-cell *matCellDef="let row" class="!text-gray-800">{{ row.Name }}</td>
              </ng-container>
              <ng-container matColumnDef="Fee">
                <th mat-header-cell *matHeaderCellDef class="!text-gray-800 !bg-gray-100">費用</th>
                <td mat-cell *matCellDef="let row" class="!text-amber-600">{{ row.Fee }}</td>
              </ng-container>
              <ng-container matColumnDef="Number">
                <th mat-header-cell *matHeaderCellDef class="!text-gray-800 !bg-gray-100">號碼</th>
                <td mat-cell *matCellDef="let row" class="!text-gray-800">{{ row.Number }}</td>
              </ng-container>
              <tr mat-header-row *matHeaderRowDef="['Name', 'Fee', 'Number']"></tr>
              <tr mat-row *matRowDef="let row; columns: ['Name', 'Fee', 'Number']"
                  class="hover:!bg-gray-50"></tr>
            </table>
          </div>

          <p class="mb-4 text-sm text-gray-800">共 {{ previewRows().length }} 筆資料</p>

          <button mat-flat-button color="primary" (click)="onImport()" [disabled]="importing()">
            {{ importing() ? '匯入中...' : '確認匯入' }}
          </button>
        }

        @if (resultMsg()) {
          <p class="mt-4 text-sm" [class]="resultSuccess() ? 'text-green-400' : 'text-red-400'">
            {{ resultMsg() }}
          </p>
        }
      </div>
    </div>
  `,
})
export class LoadPrepayComponent {
  private svc = inject(SignupsService);
  private auth = inject(AuthService);
  router = inject(Router);

  fileName = signal('');
  loading = signal(false);
  importing = signal(false);
  previewRows = signal<any[]>([]);
  resultMsg = signal('');
  resultSuccess = signal(false);

  onFileSelect(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;

    this.fileName.set(file.name);
    this.loading.set(true);
    this.resultMsg.set('');

    const reader = new FileReader();
    reader.onload = () => {
      const text = reader.result as string;
      const lines = text.split('\n').filter((l) => l.trim());
      const rows = lines.slice(1).map((line) => {
        const cols = line.split(',').map((c) => c.trim());
        return { Name: cols[0], Fee: parseInt(cols[1]) || 0, Number: parseInt(cols[2]) || 0 };
      });
      this.previewRows.set(rows);
      this.loading.set(false);
    };
    reader.readAsText(file, 'UTF-8');
  }

  async onImport(): Promise<void> {
    this.importing.set(true);
    this.resultMsg.set('');
    let successCount = 0;

    for (const row of this.previewRows()) {
      const result = await this.svc.create({
        SignupID: crypto.randomUUID(),
        Year: new Date().getFullYear() - 1911,
        SignupType: 2, // 預繳
        Name: row.Name,
        Fee: row.Fee,
        Number: row.Number,
        AdminID: this.auth.session()?.AdminID,
        Createdate: new Date().toISOString(),
      });
      if (result.success) successCount++;
    }

    this.importing.set(false);
    this.resultSuccess.set(successCount > 0);
    this.resultMsg.set(`匯入完成：成功 ${successCount} / ${this.previewRows().length} 筆`);
    this.previewRows.set([]);
  }
}
