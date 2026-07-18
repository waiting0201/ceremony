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
import { CategoryApi } from '../../core/api/categories/category.api';
import type { CategoryNode } from '../../core/api/categories/category.models';
import { ApiError } from '../../core/http/api-error';
import { NumericInputDirective } from '../../shared/directives/numeric-input.directive';

export type CategoryEditMode = 'create-root' | 'create-child' | 'edit';

export interface CategoryEditTarget {
  mode: CategoryEditMode;
  parentId: string | null;
  /** For edit mode */
  node: CategoryNode | null;
  /** Default sort value (e.g., siblings.length + 1) */
  defaultSort: number;
}

@Component({
  selector: 'app-category-edit-form',
  imports: [ReactiveFormsModule, NumericInputDirective],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './category-edit-form.component.html',
  styleUrl: './category-edit-form.component.scss',
})
export class CategoryEditFormComponent {
  private readonly api = inject(CategoryApi);
  private readonly fb = inject(FormBuilder);
  private readonly destroyRef = inject(DestroyRef);

  readonly target = input.required<CategoryEditTarget>();
  readonly saved = output<void>();
  readonly dirtyChange = output<boolean>();

  protected readonly saving = signal(false);
  protected readonly errorMessage = signal<string | null>(null);

  protected readonly form = this.fb.nonNullable.group({
    title: ['', [Validators.required, Validators.maxLength(50)]],
    sort: [1, [Validators.required, Validators.min(0)]],
  });

  protected readonly hint = computed(() => {
    const t = this.target();
    if (t.mode === 'create-child') return `父分類 ID：${t.parentId}`;
    return '';
  });

  get isDirty(): boolean { return this.form.dirty; }

  constructor() {
    effect(() => {
      const t = this.target();
      const initial = t.node
        ? { title: t.node.title, sort: t.node.sort }
        : { title: '', sort: t.defaultSort };
      this.form.reset(initial);
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
    const t = this.target();
    const { title, sort } = this.form.getRawValue();
    try {
      if (t.mode === 'edit' && t.node) {
        await this.api.update(t.node.id, { title, sort });
      } else {
        await this.api.create({ title, sort, parentId: t.parentId ?? undefined });
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

function toMessage(err: unknown): string {
  return err instanceof ApiError ? err.message : '操作失敗，請稍後再試';
}
