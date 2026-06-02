import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { IconComponent, type IconName } from '../../shared/icon/icon.component';

interface DashboardTile {
  readonly path: string;
  readonly label: string;
  readonly icon: IconName;
}

@Component({
  selector: 'app-dashboard-page',
  imports: [RouterLink, IconComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="dashboard">
      <h1>歡迎使用寶覺寺法會報名系統</h1>
      <p class="hint">請從左側選單選擇功能。常用入口：</p>
      <div class="grid">
        @for (tile of tiles; track tile.path) {
          <a class="tile" [routerLink]="tile.path">
            <app-icon class="tile-icon" [name]="tile.icon" [size]="36" />
            <span class="tile-label">{{ tile.label }}</span>
          </a>
        }
      </div>
    </div>
  `,
  styles: `
    :host { display: block; }
    .dashboard { max-width: 720px; }
    h1 {
      margin: 0 0 var(--space-sm);
      font-size: var(--font-size-xl);
      color: var(--c-text-primary);
    }
    .hint { color: var(--c-text-secondary); margin: 0 0 var(--space-lg); }
    .grid {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(180px, 1fr));
      gap: var(--space-md);
    }
    .tile {
      display: grid;
      grid-template-rows: auto auto;
      justify-items: center;
      gap: var(--space-sm);
      padding: var(--space-xl) var(--space-lg);
      background: var(--c-surface);
      border: 1px solid var(--c-border);
      border-radius: 4px;
      color: var(--c-text-primary);
      text-decoration: none;
      text-align: center;
      transition: border-color 120ms, background 120ms;
    }
    .tile:hover {
      border-color: var(--c-primary);
      background: var(--c-primary-soft);
      text-decoration: none;

      .tile-icon { color: var(--c-primary); }
    }
    .tile-icon {
      color: var(--c-text-secondary);
    }
    .tile-label {
      font-size: var(--font-size-lg);
      font-weight: 500;
    }
  `,
})
export class DashboardPage {
  protected readonly tiles: readonly DashboardTile[] = [
    { path: '/signups/new', label: '新增報名', icon: 'plus' },
    { path: '/signups', label: '報名維護', icon: 'search' },
    { path: '/believers', label: '信眾維護', icon: 'believer' },
    { path: '/prepay', label: '載入預繳', icon: 'download' },
  ];
}
