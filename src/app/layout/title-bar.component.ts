import { Component } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { IpcService } from '../core/services/ipc.service';
import { AuthService } from '../core/services/auth.service';

@Component({
  selector: 'app-title-bar',
  standalone: true,
  imports: [MatIconModule],
  template: `
    <div class="flex h-8 select-none items-center bg-white border-b border-gray-200 text-xs text-gray-800"
         style="-webkit-app-region: drag">
      <div class="flex items-center gap-2 px-3" style="-webkit-app-region: no-drag">
        <span class="text-indigo-600 font-bold">⛩</span>
        <span>法會報名系統</span>
      </div>
      <div class="flex-1"></div>
      <span class="mr-4 text-gray-500">{{ auth.adminName() }}</span>
      <div class="flex" style="-webkit-app-region: no-drag">
        <button (click)="ipc.minimize()"
                class="flex h-8 w-11 items-center justify-center text-gray-600 hover:bg-gray-200">
          <mat-icon class="!text-[16px] !w-4 !h-4">remove</mat-icon>
        </button>
        <button (click)="ipc.maximize()"
                class="flex h-8 w-11 items-center justify-center text-gray-600 hover:bg-gray-200">
          <mat-icon class="!text-[16px] !w-4 !h-4">crop_square</mat-icon>
        </button>
        <button (click)="ipc.close()"
                class="flex h-8 w-11 items-center justify-center text-gray-600 hover:bg-red-500 hover:text-white">
          <mat-icon class="!text-[16px] !w-4 !h-4">close</mat-icon>
        </button>
      </div>
    </div>
  `,
})
export class TitleBarComponent {
  constructor(
    public ipc: IpcService,
    public auth: AuthService,
  ) {}
}
