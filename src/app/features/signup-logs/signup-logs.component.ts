import { Component, inject, signal, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule, type PageEvent } from '@angular/material/paginator';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { TaiwanDatePipe } from '../../shared/pipes/taiwan-date.pipe';
import { SignupTypePipe } from '../../shared/pipes/signup-type.pipe';
import { SignupLogsService, type SignupLog } from './signup-logs.service';

@Component({
  selector: 'app-signup-logs',
  standalone: true,
  imports: [
    FormsModule, MatTableModule, MatPaginatorModule, MatFormFieldModule,
    MatInputModule, MatButtonModule, MatIconModule, MatProgressBarModule,
    TaiwanDatePipe, SignupTypePipe,
  ],
  template: `
    <div>
      <h2 class="mb-4 text-xl font-semibold text-gray-800">操作日誌</h2>

      <div class="mb-4">
        <mat-form-field appearance="outline" class="w-80">
          <mat-label>搜尋 (姓名/管理者)</mat-label>
          <input matInput [(ngModel)]="keyword" (keyup.enter)="onSearch()" />
          <button matSuffix mat-icon-button (click)="onSearch()">
            <mat-icon>search</mat-icon>
          </button>
        </mat-form-field>
      </div>

      @if (loading()) {
        <mat-progress-bar mode="indeterminate" />
      }

      <div class="overflow-hidden rounded-lg border border-gray-200">
        <table mat-table [dataSource]="logs()" class="!bg-white w-full">
          <ng-container matColumnDef="Createdate">
            <th mat-header-cell *matHeaderCellDef class="!text-gray-800 !bg-gray-100">日期</th>
            <td mat-cell *matCellDef="let row" class="!text-gray-500 text-sm">
              {{ row.Createdate | taiwanDate:'short' }}
            </td>
          </ng-container>
          <ng-container matColumnDef="Name">
            <th mat-header-cell *matHeaderCellDef class="!text-gray-800 !bg-gray-100">姓名</th>
            <td mat-cell *matCellDef="let row" class="!text-gray-800">{{ row.Name }}</td>
          </ng-container>
          <ng-container matColumnDef="CeremonyCategoryTitle">
            <th mat-header-cell *matHeaderCellDef class="!text-gray-800 !bg-gray-100">法會</th>
            <td mat-cell *matCellDef="let row" class="!text-gray-800">{{ row.CeremonyCategoryTitle }}</td>
          </ng-container>
          <ng-container matColumnDef="SignupType">
            <th mat-header-cell *matHeaderCellDef class="!text-gray-800 !bg-gray-100">類型</th>
            <td mat-cell *matCellDef="let row" class="!text-gray-800">{{ row.SignupType | signupType }}</td>
          </ng-container>
          <ng-container matColumnDef="Fee">
            <th mat-header-cell *matHeaderCellDef class="!text-gray-800 !bg-gray-100">費用</th>
            <td mat-cell *matCellDef="let row" class="!text-amber-600">{{ row.Fee }}</td>
          </ng-container>
          <ng-container matColumnDef="Admin">
            <th mat-header-cell *matHeaderCellDef class="!text-gray-800 !bg-gray-100">操作者</th>
            <td mat-cell *matCellDef="let row" class="!text-indigo-600">{{ row.Admin }}</td>
          </ng-container>

          <tr mat-header-row *matHeaderRowDef="columns"></tr>
          <tr mat-row *matRowDef="let row; columns: columns" class="hover:!bg-gray-50"></tr>
        </table>
      </div>

      <mat-paginator
        [length]="total()"
        [pageSize]="20"
        [pageSizeOptions]="[10, 20, 50]"
        (page)="onPage($event)"
        class="!bg-white !text-gray-800" />
    </div>
  `,
})
export class SignupLogsComponent implements OnInit {
  private svc = inject(SignupLogsService);

  logs = signal<SignupLog[]>([]);
  total = signal(0);
  loading = signal(false);
  keyword = '';
  page = 1;
  columns = ['Createdate', 'Name', 'CeremonyCategoryTitle', 'SignupType', 'Fee', 'Admin'];

  ngOnInit(): void { this.loadData(); }

  async loadData(): Promise<void> {
    this.loading.set(true);
    const r = await this.svc.search({ keyword: this.keyword || undefined, page: this.page, pageSize: 20 });
    if (r.success && r.data) { this.logs.set(r.data.rows); this.total.set(r.data.total); }
    this.loading.set(false);
  }

  onSearch(): void { this.page = 1; this.loadData(); }
  onPage(e: PageEvent): void { this.page = e.pageIndex + 1; this.loadData(); }
}
