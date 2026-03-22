import { Component, signal, inject, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import {
  ConfirmDialogComponent,
  type ConfirmDialogData,
} from '../../shared/components/confirm-dialog/confirm-dialog.component';
import { AdminsService, type Admin } from './admins.service';

@Component({
  selector: 'app-admins',
  standalone: true,
  imports: [
    FormsModule,
    MatTableModule,
    MatButtonModule,
    MatIconModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSlideToggleModule,
    MatProgressBarModule,
  ],
  template: `
    <div>
      <div class="mb-4 flex items-center justify-between">
        <h2 class="text-xl font-semibold text-gray-800">管理者管理</h2>
        <button mat-flat-button color="primary" (click)="openForm()">
          <mat-icon>add</mat-icon> 新增管理者
        </button>
      </div>

      @if (loading()) {
        <mat-progress-bar mode="indeterminate" />
      }

      <!-- Data Table -->
      <div class="overflow-hidden rounded-lg border border-gray-200">
        <table mat-table [dataSource]="admins()" class="!bg-white w-full">
          <ng-container matColumnDef="AdminID">
            <th mat-header-cell *matHeaderCellDef class="!text-gray-800 !bg-gray-100">ID</th>
            <td mat-cell *matCellDef="let row" class="!text-gray-800">{{ row.AdminID }}</td>
          </ng-container>

          <ng-container matColumnDef="Name">
            <th mat-header-cell *matHeaderCellDef class="!text-gray-800 !bg-gray-100">名稱</th>
            <td mat-cell *matCellDef="let row" class="!text-gray-800">{{ row.Name }}</td>
          </ng-container>

          <ng-container matColumnDef="Username">
            <th mat-header-cell *matHeaderCellDef class="!text-gray-800 !bg-gray-100">帳號</th>
            <td mat-cell *matCellDef="let row" class="!text-gray-800">{{ row.Username }}</td>
          </ng-container>

          <ng-container matColumnDef="IsEnabled">
            <th mat-header-cell *matHeaderCellDef class="!text-gray-800 !bg-gray-100">狀態</th>
            <td mat-cell *matCellDef="let row">
              <span [class]="row.IsEnabled ? 'text-green-600' : 'text-red-500'">
                {{ row.IsEnabled ? '啟用' : '停用' }}
              </span>
            </td>
          </ng-container>

          <ng-container matColumnDef="actions">
            <th mat-header-cell *matHeaderCellDef class="!text-gray-800 !bg-gray-100 !w-28">操作</th>
            <td mat-cell *matCellDef="let row">
              <button mat-icon-button (click)="openForm(row)" matTooltip="編輯">
                <mat-icon class="!text-indigo-600">edit</mat-icon>
              </button>
              <button mat-icon-button (click)="onDelete(row)" matTooltip="刪除">
                <mat-icon class="!text-red-500">delete</mat-icon>
              </button>
            </td>
          </ng-container>

          <tr mat-header-row *matHeaderRowDef="displayedColumns"></tr>
          <tr mat-row *matRowDef="let row; columns: displayedColumns"
              class="hover:!bg-gray-50"></tr>
        </table>
      </div>

      <!-- Inline Form -->
      @if (showForm()) {
        <div class="mt-4 rounded-lg border border-gray-200 bg-white p-4">
          <h3 class="mb-3 text-base font-medium text-gray-800">
            {{ editingId() ? '編輯管理者' : '新增管理者' }}
          </h3>
          <div class="grid grid-cols-2 gap-4">
            <mat-form-field appearance="outline">
              <mat-label>名稱</mat-label>
              <input matInput [(ngModel)]="formData.Name" />
            </mat-form-field>
            <mat-form-field appearance="outline">
              <mat-label>帳號</mat-label>
              <input matInput [(ngModel)]="formData.Username" />
            </mat-form-field>
            <mat-form-field appearance="outline">
              <mat-label>密碼</mat-label>
              <input matInput [(ngModel)]="formData.Password" type="password" />
            </mat-form-field>
            <div class="flex items-center">
              <mat-slide-toggle [(ngModel)]="formData.IsEnabled">
                {{ formData.IsEnabled ? '啟用' : '停用' }}
              </mat-slide-toggle>
            </div>
          </div>
          @if (formError()) {
            <p class="mb-2 text-sm text-red-400">{{ formError() }}</p>
          }
          <div class="flex gap-2">
            <button mat-flat-button color="primary" (click)="onSave()">儲存</button>
            <button mat-button (click)="closeForm()">取消</button>
          </div>
        </div>
      }
    </div>
  `,
})
export class AdminsComponent implements OnInit {
  private svc = inject(AdminsService);
  private dialog = inject(MatDialog);

  admins = signal<Admin[]>([]);
  loading = signal(false);
  showForm = signal(false);
  editingId = signal<number | null>(null);
  formError = signal('');
  displayedColumns = ['AdminID', 'Name', 'Username', 'IsEnabled', 'actions'];

  formData: Partial<Admin> = this.emptyForm();

  ngOnInit(): void {
    this.loadData();
  }

  async loadData(): Promise<void> {
    this.loading.set(true);
    const result = await this.svc.list();
    if (result.success && result.data) {
      this.admins.set(result.data);
    }
    this.loading.set(false);
  }

  openForm(admin?: Admin): void {
    if (admin) {
      this.editingId.set(admin.AdminID);
      this.formData = { ...admin };
    } else {
      this.editingId.set(null);
      this.formData = this.emptyForm();
    }
    this.formError.set('');
    this.showForm.set(true);
  }

  closeForm(): void {
    this.showForm.set(false);
    this.formError.set('');
  }

  async onSave(): Promise<void> {
    if (!this.formData.Username || !this.formData.Password) {
      this.formError.set('帳號和密碼為必填');
      return;
    }

    const id = this.editingId();
    const result = id
      ? await this.svc.update(id, this.formData)
      : await this.svc.create(this.formData);

    if (result.success) {
      this.closeForm();
      await this.loadData();
    } else {
      this.formError.set(result.message || '儲存失敗');
    }
  }

  async onDelete(admin: Admin): Promise<void> {
    const ref = this.dialog.open(ConfirmDialogComponent, {
      data: {
        title: '刪除管理者',
        message: `確定要刪除「${admin.Name || admin.Username}」嗎？`,
      } as ConfirmDialogData,
    });

    const confirmed = await ref.afterClosed().toPromise();
    if (!confirmed) return;

    const result = await this.svc.delete(admin.AdminID);
    if (result.success) {
      await this.loadData();
    }
  }

  private emptyForm(): Partial<Admin> {
    return { Name: '', Username: '', Password: '', IsEnabled: true };
  }
}
