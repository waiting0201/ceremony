import { Component, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { AuthService } from '../../core/services/auth.service';
import { IpcService } from '../../core/services/ipc.service';

interface DashCard {
  icon: string;
  title: string;
  desc: string;
  route?: string;
  action?: string;
}

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [MatIconModule, MatButtonModule],
  template: `
    <div>
      <h2 class="mb-6 text-xl font-semibold text-gray-800">
        歡迎, {{ auth.adminName() }}
      </h2>
      <div class="grid grid-cols-3 gap-4">
        @for (card of cards; track card.title) {
          <div class="rounded-lg bg-white p-5 border border-gray-200 cursor-pointer hover:shadow-md transition-shadow"
               (click)="onCardClick(card)">
            <div class="flex items-center gap-3">
              <mat-icon class="text-indigo-600">{{ card.icon }}</mat-icon>
              <span class="text-gray-800 font-medium">{{ card.title }}</span>
            </div>
            <p class="mt-2 text-sm text-gray-500">{{ card.desc }}</p>
          </div>
        }
      </div>

      @if (backupMsg()) {
        <p class="mt-4 text-sm" [class]="backupSuccess() ? 'text-green-600' : 'text-red-500'">
          {{ backupMsg() }}
        </p>
      }
    </div>
  `,
})
export class DashboardComponent {
  auth = inject(AuthService);
  private router = inject(Router);
  private ipc = inject(IpcService);

  backupMsg = signal('');
  backupSuccess = signal(false);

  cards: DashCard[] = [
    { icon: 'people', title: '信眾管理', desc: '管理信眾基本資料與通訊地址', route: '/believers' },
    { icon: 'how_to_reg', title: '報名管理', desc: '瀏覽、篩選和管理法會報名', route: '/signups' },
    { icon: 'note_add', title: '新增報名', desc: '建立新的法會報名記錄', route: '/signups/new' },
    { icon: 'account_tree', title: '法會類別', desc: '維護法會類別階層結構', route: '/ceremony-categories' },
    { icon: 'admin_panel_settings', title: '管理者', desc: '管理系統使用者帳號', route: '/admins' },
    { icon: 'backup', title: '資料庫備份', desc: '備份 SQL Server 資料庫', action: 'backup' },
  ];

  async onCardClick(card: DashCard): Promise<void> {
    if (card.route) {
      this.router.navigate([card.route]);
    } else if (card.action === 'backup') {
      await this.onBackup();
    }
  }

  async onBackup(): Promise<void> {
    this.backupMsg.set('備份中...');
    const result = await this.ipc.invoke<string>('backup:run');
    if (result.success) {
      this.backupSuccess.set(true);
      this.backupMsg.set(`備份成功: ${result.data}`);
    } else {
      this.backupSuccess.set(false);
      this.backupMsg.set(result.message || '備份失敗');
    }
  }
}
