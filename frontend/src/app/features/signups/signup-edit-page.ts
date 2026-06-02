import {
  ChangeDetectionStrategy,
  Component,
  inject,
  signal,
  ViewChild,
} from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { SignupEditFormComponent } from './signup-edit-form.component';
import { SignupSearchState } from './signup-search-state';

/**
 * Route 模式的 wrapper（deep link 用）。
 * 主要 UX 走 list page 的 overlay；此 page 為 `/signups/new`、`/signups/:id/edit` URL 仍可獨立進入。
 */
@Component({
  selector: 'app-signup-edit-page',
  imports: [RouterLink, SignupEditFormComponent],
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

  protected onCancel(): void {
    void this.router.navigateByUrl('/signups');
  }
}
