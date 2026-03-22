import { Component, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { AuthService } from '../../core/services/auth.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [FormsModule, MatFormFieldModule, MatInputModule, MatButtonModule, MatIconModule],
  template: `
    <div class="flex h-screen items-center justify-center bg-gradient-to-br from-indigo-50 via-white to-blue-50">
      <div class="w-[360px] rounded-xl bg-white p-8 shadow-lg border border-gray-100">
        <div class="mb-6 text-center">
          <h1 class="text-2xl font-bold text-gray-800">法會報名系統</h1>
          <p class="mt-1 text-sm text-gray-500">請登入以繼續</p>
        </div>

        <form (ngSubmit)="onLogin()" class="space-y-4">
          <mat-form-field appearance="outline" class="w-full">
            <mat-label>帳號</mat-label>
            <input matInput [(ngModel)]="username" name="username" autocomplete="username" />
            <mat-icon matPrefix class="text-gray-500">person</mat-icon>
          </mat-form-field>

          <mat-form-field appearance="outline" class="w-full">
            <mat-label>密碼</mat-label>
            <input matInput [(ngModel)]="password" name="password"
                   [type]="showPassword() ? 'text' : 'password'" autocomplete="current-password" />
            <mat-icon matPrefix class="text-gray-500">lock</mat-icon>
            <button mat-icon-button matSuffix type="button" (click)="showPassword.set(!showPassword())">
              <mat-icon>{{ showPassword() ? 'visibility_off' : 'visibility' }}</mat-icon>
            </button>
          </mat-form-field>

          @if (errorMessage()) {
            <p class="text-sm text-red-400">{{ errorMessage() }}</p>
          }

          <button mat-flat-button color="primary" type="submit" class="!w-full !py-2"
                  [disabled]="loading()">
            {{ loading() ? '登入中...' : '登入' }}
          </button>
        </form>
      </div>
    </div>
  `,
})
export class LoginComponent {
  username = '';
  password = '';
  showPassword = signal(false);
  loading = signal(false);
  errorMessage = signal('');

  constructor(
    private auth: AuthService,
    private router: Router,
  ) {}

  async onLogin(): Promise<void> {
    if (!this.username || !this.password) {
      this.errorMessage.set('請輸入帳號和密碼');
      return;
    }
    this.loading.set(true);
    this.errorMessage.set('');

    const result = await this.auth.login(this.username, this.password);
    this.loading.set(false);

    if (result.success) {
      this.router.navigate(['/']);
    } else {
      this.errorMessage.set(result.message || '登入失敗');
    }
  }
}
