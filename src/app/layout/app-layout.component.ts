import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { TitleBarComponent } from './title-bar.component';
import { SideMenuComponent } from './side-menu.component';

@Component({
  selector: 'app-layout',
  standalone: true,
  imports: [RouterOutlet, TitleBarComponent, SideMenuComponent],
  template: `
    <div class="flex h-screen flex-col bg-gray-50">
      <app-title-bar />
      <div class="flex flex-1 overflow-hidden">
        <app-side-menu />
        <main class="flex-1 overflow-auto bg-gray-50 p-4">
          <router-outlet />
        </main>
      </div>
      <!-- Status Bar -->
      <div class="flex h-6 items-center bg-indigo-600 px-3 text-xs text-white">
        <span>法會報名系統 v2.0.0</span>
      </div>
    </div>
  `,
})
export class AppLayoutComponent {}
