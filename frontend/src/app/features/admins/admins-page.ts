import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  OnInit,
  signal,
  ViewChild,
} from '@angular/core';
import { AdminApi } from '../../core/api/admins/admin.api';
import type { AdminListItem } from '../../core/api/admins/admin.models';
import { ApiError } from '../../core/http/api-error';
import { FormOverlayComponent } from '../../shared/form-overlay/form-overlay.component';
import { IconComponent } from '../../shared/icon/icon.component';
import { ContextMenuService } from '../../shared/context-menu/context-menu.service';
import type { ContextMenuItem } from '../../shared/context-menu/context-menu.types';
import { ConfirmDialogService } from '../../shared/confirm-dialog/confirm-dialog.service';
import { AdminEditFormComponent } from './admin-edit-form.component';

@Component({
  selector: 'app-admins-page',
  imports: [FormOverlayComponent, AdminEditFormComponent, IconComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './admins-page.html',
  styleUrl: './admins-page.scss',
})
export class AdminsPage implements OnInit {
  private readonly api = inject(AdminApi);
  private readonly menu = inject(ContextMenuService);
  private readonly confirmDialog = inject(ConfirmDialogService);

  @ViewChild(AdminEditFormComponent) protected editFormRef?: AdminEditFormComponent;

  protected readonly admins = signal<AdminListItem[]>([]);
  protected readonly loading = signal(false);
  protected readonly errorMessage = signal<string | null>(null);
  protected readonly successMessage = signal<string | null>(null);
  protected readonly editTarget = signal<AdminListItem | null>(null);
  protected readonly overlayOpen = signal(false);
  protected readonly editDirty = signal(false);

  protected readonly editTitle = computed(() =>
    this.editTarget() ? '編輯管理者' : '新增管理者',
  );

  ngOnInit(): void {
    void this.reload();
  }

  protected async reload(): Promise<void> {
    this.loading.set(true);
    this.errorMessage.set(null);
    try {
      const resp = await this.api.list();
      this.admins.set(resp.items);
    } catch (err) {
      this.errorMessage.set(toMessage(err));
    } finally {
      this.loading.set(false);
    }
  }

  protected startCreate(): void {
    this.editTarget.set(null);
    this.editDirty.set(false);
    this.overlayOpen.set(true);
    this.successMessage.set(null);
  }

  protected startEdit(admin: AdminListItem): void {
    this.editTarget.set(admin);
    this.editDirty.set(false);
    this.overlayOpen.set(true);
    this.successMessage.set(null);
  }

  protected onOverlayClose(): void {
    this.overlayOpen.set(false);
    this.editTarget.set(null);
    this.editDirty.set(false);
  }

  protected onOverlaySubmit(): void {
    void this.editFormRef?.submit();
  }

  protected async onOverlaySaved(): Promise<void> {
    const wasEdit = this.editTarget() !== null;
    this.successMessage.set(wasEdit ? '已修改管理者' : '已新增管理者');
    this.onOverlayClose();
    await this.reload();
  }

  protected onEditDirtyChange(dirty: boolean): void {
    this.editDirty.set(dirty);
  }

  protected openRowMenu(event: MouseEvent, admin: AdminListItem): void {
    event.preventDefault();
    event.stopPropagation();
    this.menu.open<AdminListItem>({
      origin: { x: event.clientX, y: event.clientY },
      items: this.buildMenuItems(),
      context: admin,
    });
  }

  protected openRowMenuFromButton(button: HTMLElement, admin: AdminListItem): void {
    this.menu.open<AdminListItem>({
      origin: button,
      items: this.buildMenuItems(),
      context: admin,
    });
  }

  private buildMenuItems(): ContextMenuItem<AdminListItem>[] {
    return [
      {
        id: 'edit',
        label: '編輯',
        icon: 'pencil',
        onClick: (admin) => this.startEdit(admin),
      },
      {
        id: 'delete',
        label: '刪除',
        icon: 'trash',
        danger: true,
        onClick: (admin) => this.actionDelete(admin),
      },
    ];
  }

  private async actionDelete(admin: AdminListItem): Promise<void> {
    const ok = await this.confirmDialog.ask({
      title: '刪除管理者',
      message: `確認刪除 ${admin.username} 嗎？`,
      confirmLabel: '確認刪除',
      danger: true,
    });
    if (!ok) return;
    this.errorMessage.set(null);
    try {
      await this.api.remove(admin.id);
      this.successMessage.set(`已刪除管理者「${admin.username}」`);
      await this.reload();
    } catch (err) {
      this.errorMessage.set(toMessage(err));
    }
  }
}

function toMessage(err: unknown): string {
  return err instanceof ApiError ? err.message : '操作失敗，請稍後再試';
}
