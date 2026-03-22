import { Component, inject, signal, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule, type PageEvent } from '@angular/material/paginator';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatDialog } from '@angular/material/dialog';
import {
  ConfirmDialogComponent,
  type ConfirmDialogData,
} from '../../shared/components/confirm-dialog/confirm-dialog.component';
import { SignupTypePipe } from '../../shared/pipes/signup-type.pipe';
import { TaiwanDatePipe } from '../../shared/pipes/taiwan-date.pipe';
import {
  SignupsService,
  type SignupView,
  type SignupSearchParams,
} from './signups.service';
import { IpcService } from '../../core/services/ipc.service';
import { ReportsService } from '../reports/reports.service';

interface CategoryOption {
  CeremonyCategoryID: string;
  Title: string;
}

@Component({
  selector: 'app-signup-list',
  standalone: true,
  imports: [
    FormsModule,
    MatTableModule,
    MatPaginatorModule,
    MatButtonModule,
    MatIconModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatProgressBarModule,
    MatTooltipModule,
    SignupTypePipe,
    TaiwanDatePipe,
  ],
  template: `
    <div>
      <div class="mb-4 flex items-center justify-between">
        <h2 class="text-xl font-semibold text-gray-800">報名管理</h2>
        <div class="flex gap-2">
          <button mat-stroked-button (click)="onExportExcel()" [disabled]="signups().length === 0">
            <mat-icon>table_chart</mat-icon> Excel
          </button>
          <button mat-flat-button color="primary" (click)="router.navigate(['/signups/new'])">
            <mat-icon>add</mat-icon> 新增報名
          </button>
        </div>
      </div>

      <!-- Filters -->
      <div class="mb-4 flex flex-wrap gap-3">
        <mat-form-field appearance="outline" class="w-28">
          <mat-label>年度</mat-label>
          <input matInput type="number" [(ngModel)]="filters.year" />
        </mat-form-field>

        <mat-form-field appearance="outline" class="w-48">
          <mat-label>法會類別</mat-label>
          <mat-select [(ngModel)]="filters.ceremonyCategoryId">
            <mat-option [value]="undefined">全部</mat-option>
            @for (c of categories(); track c.CeremonyCategoryID) {
              <mat-option [value]="c.CeremonyCategoryID">{{ c.Title }}</mat-option>
            }
          </mat-select>
        </mat-form-field>

        <mat-form-field appearance="outline" class="w-32">
          <mat-label>報名類型</mat-label>
          <mat-select [(ngModel)]="filters.signupType">
            <mat-option [value]="undefined">全部</mat-option>
            <mat-option [value]="1">一般</mat-option>
            <mat-option [value]="2">預繳</mat-option>
            <mat-option [value]="3">特殊</mat-option>
          </mat-select>
        </mat-form-field>

        <mat-form-field appearance="outline" class="w-52">
          <mat-label>關鍵字</mat-label>
          <input matInput [(ngModel)]="filters.keyword" placeholder="姓名/電話/堂名" />
        </mat-form-field>

        <button mat-flat-button color="primary" class="!h-14" (click)="onSearch()">
          <mat-icon>search</mat-icon> 搜尋
        </button>
      </div>

      @if (loading()) {
        <mat-progress-bar mode="indeterminate" />
      }

      <!-- Table -->
      <div class="overflow-hidden rounded-lg border border-gray-200">
        <table mat-table [dataSource]="signups()" class="!bg-white w-full">
          <ng-container matColumnDef="Number">
            <th mat-header-cell *matHeaderCellDef class="!text-gray-800 !bg-gray-100 !w-20">號碼</th>
            <td mat-cell *matCellDef="let row" class="!text-gray-800 font-mono">
              {{ row.NumberTitle }}{{ row.Number }}
            </td>
          </ng-container>

          <ng-container matColumnDef="CeremonyTitle">
            <th mat-header-cell *matHeaderCellDef class="!text-gray-800 !bg-gray-100">法會</th>
            <td mat-cell *matCellDef="let row" class="!text-gray-800">{{ row.CeremonyTitle }}</td>
          </ng-container>

          <ng-container matColumnDef="SignupType">
            <th mat-header-cell *matHeaderCellDef class="!text-gray-800 !bg-gray-100 !w-20">類型</th>
            <td mat-cell *matCellDef="let row" class="!text-gray-800">{{ row.SignupType | signupType }}</td>
          </ng-container>

          <ng-container matColumnDef="Name">
            <th mat-header-cell *matHeaderCellDef class="!text-gray-800 !bg-gray-100">姓名</th>
            <td mat-cell *matCellDef="let row" class="!text-gray-800 font-medium">{{ row.Name }}</td>
          </ng-container>

          <ng-container matColumnDef="Fee">
            <th mat-header-cell *matHeaderCellDef class="!text-gray-800 !bg-gray-100 !w-20">費用</th>
            <td mat-cell *matCellDef="let row" class="!text-amber-600">{{ row.Fee }}</td>
          </ng-container>

          <ng-container matColumnDef="Createdate">
            <th mat-header-cell *matHeaderCellDef class="!text-gray-800 !bg-gray-100 !w-32">建立日期</th>
            <td mat-cell *matCellDef="let row" class="!text-gray-500 text-sm">
              {{ row.Createdate | taiwanDate:'short' }}
            </td>
          </ng-container>

          <ng-container matColumnDef="actions">
            <th mat-header-cell *matHeaderCellDef class="!text-gray-800 !bg-gray-100 !w-28">操作</th>
            <td mat-cell *matCellDef="let row">
              <button mat-icon-button matTooltip="編輯"
                      (click)="router.navigate(['/signups/edit', row.SignupID])">
                <mat-icon class="!text-indigo-600">edit</mat-icon>
              </button>
              <button mat-icon-button matTooltip="刪除" (click)="onDelete(row)">
                <mat-icon class="!text-red-500">delete</mat-icon>
              </button>
            </td>
          </ng-container>

          <tr mat-header-row *matHeaderRowDef="displayedColumns"></tr>
          <tr mat-row *matRowDef="let row; columns: displayedColumns"
              class="hover:!bg-gray-50"></tr>
        </table>
      </div>

      <mat-paginator
        [length]="total()"
        [pageSize]="pageSize"
        [pageSizeOptions]="[10, 20, 50]"
        (page)="onPage($event)"
        class="!bg-white !text-gray-800" />
    </div>
  `,
})
export class SignupListComponent implements OnInit {
  private svc = inject(SignupsService);
  private ipc = inject(IpcService);
  private dialog = inject(MatDialog);
  private reportsSvc = inject(ReportsService);
  router = inject(Router);

  signups = signal<SignupView[]>([]);
  categories = signal<CategoryOption[]>([]);
  total = signal(0);
  loading = signal(false);
  pageSize = 20;

  filters: SignupSearchParams = {
    year: new Date().getFullYear() - 1911,
    page: 1,
    pageSize: 20,
  };

  displayedColumns = ['Number', 'CeremonyTitle', 'SignupType', 'Name', 'Fee', 'Createdate', 'actions'];

  async ngOnInit(): Promise<void> {
    const catResult = await this.ipc.invoke<CategoryOption[]>('ceremony-categories:list');
    if (catResult.success && catResult.data) {
      this.categories.set(catResult.data);
    }
    this.loadData();
  }

  async loadData(): Promise<void> {
    this.loading.set(true);
    const result = await this.svc.search(this.filters);
    if (result.success && result.data) {
      this.signups.set(result.data.rows);
      this.total.set(result.data.total);
    }
    this.loading.set(false);
  }

  onSearch(): void {
    this.filters.page = 1;
    this.loadData();
  }

  onPage(event: PageEvent): void {
    this.filters.page = event.pageIndex + 1;
    this.filters.pageSize = event.pageSize;
    this.pageSize = event.pageSize;
    this.loadData();
  }

  async onDelete(signup: SignupView): Promise<void> {
    const ref = this.dialog.open(ConfirmDialogComponent, {
      data: {
        title: '刪除報名',
        message: `確定要刪除「${signup.Name}」的報名記錄嗎？`,
      } as ConfirmDialogData,
    });
    const confirmed = await ref.afterClosed().toPromise();
    if (!confirmed) return;

    const result = await this.svc.delete(signup.SignupID);
    if (result.success) await this.loadData();
  }

  async onExportExcel(): Promise<void> {
    const columns = [
      { header: '號碼', key: 'Number', width: 10 },
      { header: '法會', key: 'CeremonyTitle', width: 20 },
      { header: '姓名', key: 'Name', width: 15 },
      { header: '費用', key: 'Fee', width: 10 },
      { header: '電話', key: 'Phone', width: 15 },
      { header: '堂名', key: 'HallName', width: 10 },
      { header: '備註', key: 'Remark', width: 25 },
    ];
    await this.reportsSvc.exportExcel(this.signups(), columns, '報名清單');
  }
}
