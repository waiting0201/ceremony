import { Component, inject, signal, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule, type PageEvent } from '@angular/material/paginator';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatDialog } from '@angular/material/dialog';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import {
  ConfirmDialogComponent,
  type ConfirmDialogData,
} from '../../shared/components/confirm-dialog/confirm-dialog.component';
import { BelieversService, type Believer } from './believers.service';
import { BelieverDetailComponent } from './believer-detail.component';

@Component({
  selector: 'app-believers',
  standalone: true,
  imports: [
    FormsModule,
    MatTableModule,
    MatPaginatorModule,
    MatButtonModule,
    MatIconModule,
    MatFormFieldModule,
    MatInputModule,
    MatProgressBarModule,
    MatTooltipModule,
  ],
  template: `
    <div>
      <div class="mb-4 flex items-center justify-between">
        <h2 class="text-xl font-semibold text-gray-800">信眾管理</h2>
        <button mat-flat-button color="primary" (click)="openDetail(null)">
          <mat-icon>add</mat-icon> 新增信眾
        </button>
      </div>

      <!-- Search -->
      <div class="mb-4">
        <mat-form-field appearance="outline" class="w-80">
          <mat-label>搜尋 (姓名/電話/堂名)</mat-label>
          <input matInput [(ngModel)]="keyword" (keyup.enter)="onSearch()" />
          <button matSuffix mat-icon-button (click)="onSearch()">
            <mat-icon>search</mat-icon>
          </button>
        </mat-form-field>
      </div>

      @if (loading()) {
        <mat-progress-bar mode="indeterminate" />
      }

      <!-- Table -->
      <div class="overflow-hidden rounded-lg border border-gray-200">
        <table mat-table [dataSource]="believers()" class="!bg-white w-full">
          <ng-container matColumnDef="Name">
            <th mat-header-cell *matHeaderCellDef class="!text-gray-800 !bg-gray-100">姓名</th>
            <td mat-cell *matCellDef="let row" class="!text-gray-800 font-medium">{{ row.Name }}</td>
          </ng-container>

          <ng-container matColumnDef="HallName">
            <th mat-header-cell *matHeaderCellDef class="!text-gray-800 !bg-gray-100">堂名</th>
            <td mat-cell *matCellDef="let row" class="!text-gray-800">{{ row.HallName }}</td>
          </ng-container>

          <ng-container matColumnDef="Phone">
            <th mat-header-cell *matHeaderCellDef class="!text-gray-800 !bg-gray-100">電話</th>
            <td mat-cell *matCellDef="let row" class="!text-gray-800">{{ row.Phone }}</td>
          </ng-container>

          <ng-container matColumnDef="MailAddress">
            <th mat-header-cell *matHeaderCellDef class="!text-gray-800 !bg-gray-100">郵寄地址</th>
            <td mat-cell *matCellDef="let row" class="!text-gray-800 text-sm">
              {{ row.MailCity }}{{ row.MailArea }} {{ row.MailAddress }}
            </td>
          </ng-container>

          <ng-container matColumnDef="actions">
            <th mat-header-cell *matHeaderCellDef class="!text-gray-800 !bg-gray-100 !w-28">操作</th>
            <td mat-cell *matCellDef="let row">
              <button mat-icon-button (click)="openDetail(row)" matTooltip="編輯">
                <mat-icon class="!text-indigo-600">edit</mat-icon>
              </button>
              <button mat-icon-button (click)="onDelete(row)" matTooltip="刪除">
                <mat-icon class="!text-red-500">delete</mat-icon>
              </button>
            </td>
          </ng-container>

          <tr mat-header-row *matHeaderRowDef="displayedColumns"></tr>
          <tr mat-row *matRowDef="let row; columns: displayedColumns"
              class="hover:!bg-gray-50 cursor-pointer"
              (dblclick)="openDetail(row)"></tr>
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
export class BelieversComponent implements OnInit {
  private svc = inject(BelieversService);
  private dialog = inject(MatDialog);

  believers = signal<Believer[]>([]);
  total = signal(0);
  loading = signal(false);
  keyword = '';
  page = 1;
  pageSize = 20;
  displayedColumns = ['Name', 'HallName', 'Phone', 'MailAddress', 'actions'];

  ngOnInit(): void {
    this.loadData();
  }

  async loadData(): Promise<void> {
    this.loading.set(true);
    const result = await this.svc.search({
      keyword: this.keyword || undefined,
      page: this.page,
      pageSize: this.pageSize,
    });
    if (result.success && result.data) {
      this.believers.set(result.data.rows);
      this.total.set(result.data.total);
    }
    this.loading.set(false);
  }

  onSearch(): void {
    this.page = 1;
    this.loadData();
  }

  onPage(event: PageEvent): void {
    this.page = event.pageIndex + 1;
    this.pageSize = event.pageSize;
    this.loadData();
  }

  async openDetail(believer: Believer | null): Promise<void> {
    const ref = this.dialog.open(BelieverDetailComponent, {
      data: believer,
      width: '900px',
      maxHeight: '90vh',
    });

    const saved = await ref.afterClosed().toPromise();
    if (saved) {
      await this.loadData();
    }
  }

  async onDelete(believer: Believer): Promise<void> {
    const ref = this.dialog.open(ConfirmDialogComponent, {
      data: {
        title: '刪除信眾',
        message: `確定要刪除「${believer.Name}」嗎？相關的報名記錄不會被刪除。`,
      } as ConfirmDialogData,
    });

    const confirmed = await ref.afterClosed().toPromise();
    if (!confirmed) return;

    const result = await this.svc.delete(believer.BelieverID);
    if (result.success) {
      await this.loadData();
    }
  }
}
