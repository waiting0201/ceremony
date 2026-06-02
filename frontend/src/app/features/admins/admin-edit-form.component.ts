import {
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  effect,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { AdminApi } from '../../core/api/admins/admin.api';
import type { AdminListItem } from '../../core/api/admins/admin.models';
import { ApiError } from '../../core/http/api-error';

/**
 * 管理者 create/edit form。
 * - admin 有值 → 編輯模式：username disabled、password 選填（空白代表不變更）
 * - admin 為 null → 新增模式：username 必填、password 必填
 */
@Component({
  selector: 'app-admin-edit-form',
  imports: [ReactiveFormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './admin-edit-form.component.html',
  styleUrl: './admin-edit-form.component.scss',
})
export class AdminEditFormComponent {
  private readonly api = inject(AdminApi);
  private readonly fb = inject(FormBuilder);
  private readonly destroyRef = inject(DestroyRef);

  readonly admin = input<AdminListItem | null>(null);
  readonly saved = output<void>();
  readonly dirtyChange = output<boolean>();

  protected readonly saving = signal(false);
  protected readonly errorMessage = signal<string | null>(null);

  protected readonly mode = computed<'create' | 'edit'>(() =>
    this.admin() ? 'edit' : 'create',
  );

  protected readonly form = this.fb.nonNullable.group(
    {
      username: [{ value: '', disabled: false }, [Validators.required, Validators.maxLength(50)]],
      name: ['', [Validators.maxLength(50)]],
      password: ['', [Validators.minLength(4)]],
      confirmPassword: [''],
    },
    { validators: [matchPasswords] },
  );

  get isDirty(): boolean { return this.form.dirty; }

  constructor() {
    effect(() => {
      const a = this.admin();
      const usernameCtrl = this.form.controls.username;
      const passwordCtrl = this.form.controls.password;
      const confirmCtrl = this.form.controls.confirmPassword;

      if (a) {
        // 編輯模式：username 不可變、password 選填
        this.form.reset({ username: a.username, name: a.name ?? '', password: '', confirmPassword: '' });
        usernameCtrl.disable({ emitEvent: false });
        passwordCtrl.clearValidators();
        passwordCtrl.addValidators(Validators.minLength(4));
        confirmCtrl.clearValidators();
      } else {
        this.form.reset({ username: '', name: '', password: '', confirmPassword: '' });
        usernameCtrl.enable({ emitEvent: false });
        passwordCtrl.setValidators([Validators.required, Validators.minLength(4)]);
        confirmCtrl.setValidators([Validators.required]);
      }
      passwordCtrl.updateValueAndValidity({ emitEvent: false });
      confirmCtrl.updateValueAndValidity({ emitEvent: false });
      this.form.markAsPristine();
    });

    this.form.valueChanges
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.dirtyChange.emit(this.form.dirty));
  }

  async submit(): Promise<void> {
    if (this.form.invalid || this.saving()) return;
    this.saving.set(true);
    this.errorMessage.set(null);
    const { username, name, password } = this.form.getRawValue();
    try {
      const existing = this.admin();
      if (existing) {
        await this.api.update(existing.id, {
          name: name || null,
          password: password ? password : null,
        });
      } else {
        await this.api.create({ username, name: name || null, password });
      }
      this.form.markAsPristine();
      this.saved.emit();
    } catch (err) {
      this.errorMessage.set(toMessage(err));
    } finally {
      this.saving.set(false);
    }
  }

  protected onSubmitFromForm(): void { void this.submit(); }
}

function matchPasswords(group: {
  get: (k: string) => { value: unknown } | null;
}): Record<string, boolean> | null {
  const p = group.get('password')?.value as string | undefined;
  const c = group.get('confirmPassword')?.value as string | undefined;
  return p && c !== p ? { passwordMismatch: true } : null;
}

function toMessage(err: unknown): string {
  return err instanceof ApiError ? err.message : '操作失敗，請稍後再試';
}
