import { Component, inject, signal, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatTreeModule, MatTreeNestedDataSource } from '@angular/material/tree';
import { NestedTreeControl } from '@angular/cdk/tree';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatDialog } from '@angular/material/dialog';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import {
  ConfirmDialogComponent,
  type ConfirmDialogData,
} from '../../shared/components/confirm-dialog/confirm-dialog.component';
import {
  CeremonyCategoriesService,
  type CategoryTreeNode,
} from './ceremony-categories.service';

@Component({
  selector: 'app-ceremony-categories',
  standalone: true,
  imports: [
    FormsModule,
    MatTreeModule,
    MatIconModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatProgressBarModule,
  ],
  template: `
    <div>
      <div class="mb-4 flex items-center justify-between">
        <h2 class="text-xl font-semibold text-gray-800">法會類別</h2>
        <button mat-flat-button color="primary" (click)="openForm(null)">
          <mat-icon>add</mat-icon> 新增根類別
        </button>
      </div>

      @if (loading()) {
        <mat-progress-bar mode="indeterminate" />
      }

      <div class="rounded-lg border border-gray-200 bg-white p-4">
        <mat-tree [dataSource]="dataSource" [treeControl]="treeControl">
          <!-- Leaf node -->
          <mat-tree-node *matTreeNodeDef="let node" matTreeNodePadding class="!min-h-10">
            <div class="flex w-full items-center gap-2 rounded px-2 py-1 hover:bg-gray-100">
              <span class="w-6"></span>
              <mat-icon class="!text-[18px] text-amber-600">label</mat-icon>
              <span class="flex-1 text-gray-800">{{ node.Title }}</span>
              <span class="text-xs text-gray-500">排序: {{ node.Sort }}</span>
              <button mat-icon-button (click)="openForm(node.CeremonyCategoryID)">
                <mat-icon class="!text-[16px] !text-indigo-600">add</mat-icon>
              </button>
              <button mat-icon-button (click)="openEditForm(node)">
                <mat-icon class="!text-[16px] !text-indigo-600">edit</mat-icon>
              </button>
              <button mat-icon-button (click)="onDelete(node)">
                <mat-icon class="!text-[16px] !text-red-500">delete</mat-icon>
              </button>
            </div>
          </mat-tree-node>

          <!-- Parent node -->
          <mat-nested-tree-node *matTreeNodeDef="let node; when: hasChildren">
            <div class="flex w-full items-center gap-2 rounded px-2 py-1 hover:bg-gray-100 !min-h-10">
              <button mat-icon-button matTreeNodeToggle>
                <mat-icon>
                  {{ treeControl.isExpanded(node) ? 'expand_more' : 'chevron_right' }}
                </mat-icon>
              </button>
              <mat-icon class="!text-[18px] text-amber-600">folder</mat-icon>
              <span class="flex-1 font-medium text-gray-800">{{ node.Title }}</span>
              <span class="text-xs text-gray-500">排序: {{ node.Sort }}</span>
              <button mat-icon-button (click)="openForm(node.CeremonyCategoryID)">
                <mat-icon class="!text-[16px] !text-indigo-600">add</mat-icon>
              </button>
              <button mat-icon-button (click)="openEditForm(node)">
                <mat-icon class="!text-[16px] !text-indigo-600">edit</mat-icon>
              </button>
              <button mat-icon-button (click)="onDelete(node)">
                <mat-icon class="!text-[16px] !text-red-500">delete</mat-icon>
              </button>
            </div>
            <div [class.hidden]="!treeControl.isExpanded(node)" class="pl-4">
              <ng-container matTreeNodeOutlet></ng-container>
            </div>
          </mat-nested-tree-node>
        </mat-tree>

        @if (!loading() && dataSource.data.length === 0) {
          <p class="py-8 text-center text-gray-500">尚無法會類別，請點擊「新增根類別」開始建立</p>
        }
      </div>

      <!-- Form -->
      @if (showForm()) {
        <div class="mt-4 rounded-lg border border-gray-200 bg-white p-4">
          <h3 class="mb-3 text-base font-medium text-gray-800">
            {{ editingNode() ? '編輯類別' : '新增類別' }}
          </h3>
          <div class="flex gap-4">
            <mat-form-field appearance="outline" class="flex-1">
              <mat-label>類別名稱</mat-label>
              <input matInput [(ngModel)]="formTitle" />
            </mat-form-field>
            <mat-form-field appearance="outline" class="w-28">
              <mat-label>排序</mat-label>
              <input matInput type="number" [(ngModel)]="formSort" />
            </mat-form-field>
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
export class CeremonyCategoriesComponent implements OnInit {
  private svc = inject(CeremonyCategoriesService);
  private dialog = inject(MatDialog);

  loading = signal(false);
  showForm = signal(false);
  formError = signal('');
  editingNode = signal<CategoryTreeNode | null>(null);
  formParentId: string | null = null;
  formTitle = '';
  formSort = 1;

  treeControl = new NestedTreeControl<CategoryTreeNode>((node) => node.children);
  dataSource = new MatTreeNestedDataSource<CategoryTreeNode>();

  hasChildren = (_: number, node: CategoryTreeNode): boolean => node.children?.length > 0;

  ngOnInit(): void {
    this.loadTree();
  }

  async loadTree(): Promise<void> {
    this.loading.set(true);
    const result = await this.svc.getTree();
    if (result.success && result.data) {
      this.dataSource.data = result.data;
    }
    this.loading.set(false);
  }

  async openForm(parentId: string | null): Promise<void> {
    this.editingNode.set(null);
    this.formParentId = parentId;
    this.formTitle = '';
    this.formError.set('');

    const sortResult = await this.svc.getNextSort(parentId);
    this.formSort = sortResult.success && sortResult.data ? sortResult.data : 1;
    this.showForm.set(true);
  }

  openEditForm(node: CategoryTreeNode): void {
    this.editingNode.set(node);
    this.formParentId = node.ParentID;
    this.formTitle = node.Title;
    this.formSort = node.Sort;
    this.formError.set('');
    this.showForm.set(true);
  }

  closeForm(): void {
    this.showForm.set(false);
    this.formError.set('');
  }

  async onSave(): Promise<void> {
    if (!this.formTitle.trim()) {
      this.formError.set('類別名稱為必填');
      return;
    }

    const editing = this.editingNode();
    const data = {
      Title: this.formTitle.trim(),
      ParentID: this.formParentId,
      Sort: this.formSort,
    };

    const result = editing
      ? await this.svc.update(editing.CeremonyCategoryID, data)
      : await this.svc.create({
          ...data,
          CeremonyCategoryID: crypto.randomUUID(),
        });

    if (result.success) {
      this.closeForm();
      await this.loadTree();
    } else {
      this.formError.set(result.message || '儲存失敗');
    }
  }

  async onDelete(node: CategoryTreeNode): Promise<void> {
    const hasChildren = node.children?.length > 0;
    const ref = this.dialog.open(ConfirmDialogComponent, {
      data: {
        title: '刪除類別',
        message: hasChildren
          ? `「${node.Title}」下有子類別，確定要刪除嗎？（子類別不會被刪除，會變成根類別）`
          : `確定要刪除「${node.Title}」嗎？`,
      } as ConfirmDialogData,
    });

    const confirmed = await ref.afterClosed().toPromise();
    if (!confirmed) return;

    const result = await this.svc.delete(node.CeremonyCategoryID);
    if (result.success) {
      await this.loadTree();
    }
  }
}
