import {
  ChangeDetectionStrategy,
  Component,
  inject,
  OnInit,
  signal,
  ViewChild,
} from '@angular/core';
import { CategoryApi } from '../../core/api/categories/category.api';
import type { CategoryNode } from '../../core/api/categories/category.models';
import { ApiError } from '../../core/http/api-error';
import { FormOverlayComponent } from '../../shared/form-overlay/form-overlay.component';
import { ConfirmDialogService } from '../../shared/confirm-dialog/confirm-dialog.service';
import {
  CategoryEditFormComponent,
  type CategoryEditTarget,
} from './category-edit-form.component';

@Component({
  selector: 'app-categories-page',
  imports: [FormOverlayComponent, CategoryEditFormComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './categories-page.html',
  styleUrl: './categories-page.scss',
})
export class CategoriesPage implements OnInit {
  private readonly api = inject(CategoryApi);
  private readonly confirmDialog = inject(ConfirmDialogService);

  @ViewChild(CategoryEditFormComponent) protected editFormRef?: CategoryEditFormComponent;

  protected readonly tree = signal<CategoryNode[]>([]);
  protected readonly loading = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly editTarget = signal<CategoryEditTarget | null>(null);
  protected readonly editDirty = signal(false);

  ngOnInit(): void {
    void this.reload();
  }

  protected async reload(): Promise<void> {
    this.loading.set(true);
    this.errorMessage.set(null);
    try {
      const resp = await this.api.list();
      this.tree.set(resp.items);
    } catch (err) {
      this.errorMessage.set(toMessage(err));
    } finally {
      this.loading.set(false);
    }
  }

  protected startCreateRoot(): void {
    this.editTarget.set({
      mode: 'create-root',
      parentId: null,
      node: null,
      defaultSort: this.tree().length + 1,
    });
    this.editDirty.set(false);
  }

  protected startCreateChild(parent: CategoryNode): void {
    this.editTarget.set({
      mode: 'create-child',
      parentId: parent.id,
      node: null,
      defaultSort: parent.children.length + 1,
    });
    this.editDirty.set(false);
  }

  protected startEdit(node: CategoryNode): void {
    this.editTarget.set({
      mode: 'edit',
      parentId: null,
      node,
      defaultSort: node.sort,
    });
    this.editDirty.set(false);
  }

  protected onOverlayClose(): void {
    this.editTarget.set(null);
    this.editDirty.set(false);
  }

  protected onOverlaySubmit(): void {
    void this.editFormRef?.submit();
  }

  protected async onOverlaySaved(): Promise<void> {
    this.onOverlayClose();
    await this.reload();
  }

  protected onEditDirtyChange(dirty: boolean): void {
    this.editDirty.set(dirty);
  }

  protected editTitle(): string {
    const t = this.editTarget();
    if (!t) return '';
    if (t.mode === 'create-root') return '新增根分類';
    if (t.mode === 'create-child') return '新增子分類';
    return '編輯分類';
  }

  protected async remove(node: CategoryNode): Promise<void> {
    const ok = await this.confirmDialog.ask({
      title: '刪除分類',
      message: `將刪除「${node.title}」，不可復原，確定？`,
      confirmLabel: '確認刪除',
      danger: true,
    });
    if (!ok) return;
    this.errorMessage.set(null);
    try {
      await this.api.remove(node.id);
      await this.reload();
    } catch (err) {
      this.errorMessage.set(toMessage(err));
    }
  }
}

function toMessage(err: unknown): string {
  return err instanceof ApiError ? err.message : '操作失敗，請稍後再試';
}
