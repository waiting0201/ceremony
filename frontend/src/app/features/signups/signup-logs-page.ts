import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  OnInit,
  signal,
} from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { SignupApi } from '../../core/api/signups/signup.api';
import type { SignupLogItem } from '../../core/api/signups/signup.models';
import { ApiError } from '../../core/http/api-error';
import { AvoidFourPipe } from '../../shared/pipes/avoid-four.pipe';
import { signupTypeLabel } from '../../shared/util/signup-type';

@Component({
  selector: 'app-signup-logs-page',
  imports: [RouterLink, AvoidFourPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './signup-logs-page.html',
  styleUrl: './signup-logs-page.scss',
})
export class SignupLogsPage implements OnInit {
  private readonly api = inject(SignupApi);
  private readonly route = inject(ActivatedRoute);

  protected readonly logs = signal<SignupLogItem[]>([]);
  protected readonly loading = signal(false);
  protected readonly errorMessage = signal<string | null>(null);

  protected readonly signupId = computed(() =>
    this.route.snapshot.paramMap.get('id') ?? '',
  );

  protected readonly signupTypeLabel = signupTypeLabel;

  ngOnInit(): void {
    void this.load();
  }

  protected async load(): Promise<void> {
    const id = this.signupId();
    if (!id) {
      this.errorMessage.set('缺少 signup id');
      return;
    }
    this.loading.set(true);
    this.errorMessage.set(null);
    try {
      const resp = await this.api.listLogs(id);
      this.logs.set(resp.items);
    } catch (err) {
      this.errorMessage.set(err instanceof ApiError ? err.message : '載入失敗');
    } finally {
      this.loading.set(false);
    }
  }
}
