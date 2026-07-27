import {
  ChangeDetectionStrategy,
  Component,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { SignupEditFormComponent, type SignupSavedEvent } from './signup-edit-form.component';
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

  // signal query：按鈕列要即時反映表單狀態（列印資料卡的 disabled），@ViewChild 不是 reactive。
  protected readonly formRef = viewChild(SignupEditFormComponent);

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
    void this.formRef()?.submit();
  }

  /**
   * 存檔成功。新增類（`keepOpen`）留在原頁、資料原樣保留（2026-07-27 使用者指定，對齊舊
   * `NewSignupForm` 成功後不清表單）；編輯/插入維持返回列表。列表下次 mount 由 stale 旗標重查。
   */
  protected onSaved(e: SignupSavedEvent): void {
    this.state.markStale();
    if (!e.keepOpen) void this.router.navigateByUrl('/signups');
  }

  /** 列印剛新增那筆的資料卡（按鈕在「取消」左邊，對齊舊 btnPrintDataCard）。 */
  protected onPrintDataCard(): void {
    void this.formRef()?.printDataCard();
  }

  /**
   * 取消（2026-07-21 使用者指定「按取消不能跳頁」）：
   * 新增模式＝清成新的一筆（保留法會資料、不跳頁）；編輯模式＝返回列表。
   */
  protected onCancel(): void {
    if (this.signupId()) {
      void this.router.navigateByUrl('/signups');
    } else {
      this.formRef()?.resetBelow();
    }
  }
}
