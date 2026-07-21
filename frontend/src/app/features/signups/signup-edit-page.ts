import {
  ChangeDetectionStrategy,
  Component,
  inject,
  signal,
  ViewChild,
} from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { SignupEditFormComponent } from './signup-edit-form.component';
import { SignupSearchState } from './signup-search-state';

/**
 * Route 模式的 wrapper（deep link 用）。
 * 主要 UX 走 list page 的 overlay；此 page 為 `/signups/new`、`/signups/:id/edit` URL 仍可獨立進入。
 */
@Component({
  selector: 'app-signup-edit-page',
  imports: [SignupEditFormComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './signup-edit-page.html',
  styleUrl: './signup-edit-page.scss',
})
export class SignupEditPage {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly state = inject(SignupSearchState);

  @ViewChild(SignupEditFormComponent) protected formRef?: SignupEditFormComponent;

  protected readonly signupId = signal<string | null>(
    this.route.snapshot.paramMap.get('id'),
  );
  protected readonly fromSignupId = signal<string | null>(
    this.route.snapshot.queryParamMap.get('fromSignupId'),
  );
  protected readonly title = signal<string>(
    this.signupId() ? '編輯報名' : '新增報名',
  );

  protected onSubmit(): void {
    void this.formRef?.submit();
  }

  protected onSaved(): void {
    this.state.markStale();
    void this.router.navigateByUrl('/signups');
  }

  /**
   * 取消（2026-07-21 使用者指定「按取消不能跳頁」）：
   * 新增模式＝清成新的一筆（保留法會資料、不跳頁）；編輯模式＝返回列表。
   */
  protected onCancel(): void {
    if (this.signupId()) {
      void this.router.navigateByUrl('/signups');
    } else {
      this.formRef?.resetBelow();
    }
  }
}
