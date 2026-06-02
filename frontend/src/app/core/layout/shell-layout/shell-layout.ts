import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthStore } from '../../auth/auth.store';
import { IconComponent, type IconName } from '../../../shared/icon/icon.component';
import { environment } from '../../../../environments/environment';

interface NavItem {
  readonly path: string;
  readonly label: string;
  readonly icon: IconName;
  readonly exact?: boolean;
}

@Component({
  selector: 'app-shell-layout',
  imports: [RouterLink, RouterLinkActive, RouterOutlet, IconComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './shell-layout.html',
  styleUrl: './shell-layout.scss',
})
export class ShellLayout {
  private readonly router = inject(Router);
  protected readonly auth = inject(AuthStore);
  protected readonly appVersion = environment.version;

  protected readonly navItems: readonly NavItem[] = [
    { path: '/believers', label: '信眾維護', icon: 'believer' },
    { path: '/signups/new', label: '新增報名', icon: 'plus', exact: true },
    { path: '/signups', label: '報名維護', icon: 'search', exact: true },
    { path: '/prepay', label: '載入預繳', icon: 'download' },
    { path: '/backup', label: '資料備份', icon: 'database' },
    { path: '/categories', label: '法會類型', icon: 'category' },
    { path: '/reports/preview', label: '列印預覽', icon: 'printer' },
    { path: '/admins', label: '管理者', icon: 'settings' },
  ];

  protected async onLogout(): Promise<void> {
    await this.auth.logout();
    void this.router.navigateByUrl('/login');
  }
}
