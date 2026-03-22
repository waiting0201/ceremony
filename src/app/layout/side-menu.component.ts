import { Component, signal } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { AuthService } from '../core/services/auth.service';
import { Router } from '@angular/router';

interface MenuItem {
  icon: string;
  label: string;
  route: string;
}

@Component({
  selector: 'app-side-menu',
  standalone: true,
  imports: [RouterLink, RouterLinkActive, MatIconModule, MatTooltipModule],
  template: `
    <div class="flex h-full">
      <!-- Activity Bar -->
      <div class="flex w-12 flex-col items-center bg-indigo-700 py-2">
        @for (item of menuItems; track item.route) {
          <a [routerLink]="item.route" routerLinkActive="!border-l-2 !border-white !text-white"
             [matTooltip]="item.label" matTooltipPosition="right"
             class="flex h-12 w-full items-center justify-center text-white/60 hover:text-white border-l-2 border-transparent">
            <mat-icon>{{ item.icon }}</mat-icon>
          </a>
        }
        <div class="flex-1"></div>
        <button (click)="onLogout()"
                [matTooltip]="'登出'" matTooltipPosition="right"
                class="flex h-12 w-full items-center justify-center text-white/60 hover:text-white">
          <mat-icon>logout</mat-icon>
        </button>
      </div>

      <!-- Side Panel -->
      <div class="w-48 bg-white border-r border-gray-200">
        <div class="px-4 py-3 text-[11px] font-semibold uppercase tracking-wider text-gray-600">
          功能選單
        </div>
        @for (item of menuItems; track item.route) {
          <a [routerLink]="item.route" routerLinkActive="!bg-indigo-50 !text-indigo-700"
             class="flex items-center gap-2 px-4 py-2 text-sm text-gray-800 hover:bg-gray-100">
            <mat-icon class="!text-[18px] !w-[18px] !h-[18px]">{{ item.icon }}</mat-icon>
            <span>{{ item.label }}</span>
          </a>
        }
      </div>
    </div>
  `,
})
export class SideMenuComponent {
  menuItems: MenuItem[] = [
    { icon: 'dashboard', label: '首頁', route: '/dashboard' },
    { icon: 'admin_panel_settings', label: '管理者', route: '/admins' },
    { icon: 'people', label: '信眾管理', route: '/believers' },
    { icon: 'account_tree', label: '法會類別', route: '/ceremony-categories' },
    { icon: 'how_to_reg', label: '報名管理', route: '/signups' },
    { icon: 'note_add', label: '新增報名', route: '/signups/new' },
    { icon: 'upload_file', label: '匯入預付款', route: '/signups/prepay' },
    { icon: 'history', label: '操作日誌', route: '/signup-logs' },
    { icon: 'print', label: '報表預覽', route: '/reports' },
  ];

  constructor(
    private auth: AuthService,
    private router: Router,
  ) {}

  async onLogout(): Promise<void> {
    await this.auth.logout();
    this.router.navigate(['/login']);
  }
}
