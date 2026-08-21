import { ComponentPortal } from '@angular/cdk/portal';
import { Overlay } from '@angular/cdk/overlay';
import { inject, Injectable } from '@angular/core';
import { ConfirmDialogComponent } from './confirm-dialog.component';
import type { ConfirmDialogConfig } from './confirm-dialog.types';

@Injectable({ providedIn: 'root' })
export class ConfirmDialogService {
  private readonly overlay = inject(Overlay);

  /**
   * 帶一格數字輸入的對話框（config.numberInput 必填）。
   * 回傳輸入值；取消 / Esc / 點背景 → null。
   */
  askNumber(config: ConfirmDialogConfig & { numberInput: NonNullable<ConfirmDialogConfig['numberInput']> }): Promise<number | null> {
    return new Promise((resolve) => {
      const overlayRef = this.overlay.create({
        hasBackdrop: false,
        scrollStrategy: this.overlay.scrollStrategies.block(),
        positionStrategy: this.overlay.position().global(),
      });
      const ref = overlayRef.attach(new ComponentPortal(ConfirmDialogComponent));
      ref.setInput('config', config);

      const close = (result: number | null) => {
        overlayRef.dispose();
        resolve(result);
      };

      ref.instance.confirm.subscribe(() => close(ref.instance.value()));
      ref.instance.cancel.subscribe(() => close(null));
    });
  }

  ask(config: ConfirmDialogConfig): Promise<boolean> {
    return new Promise((resolve) => {
      const overlayRef = this.overlay.create({
        hasBackdrop: false,
        scrollStrategy: this.overlay.scrollStrategies.block(),
        positionStrategy: this.overlay.position().global(),
      });
      const portal = new ComponentPortal(ConfirmDialogComponent);
      const ref = overlayRef.attach(portal);
      ref.setInput('config', config);

      const close = (result: boolean) => {
        overlayRef.dispose();
        resolve(result);
      };

      ref.instance.confirm.subscribe(() => close(true));
      ref.instance.cancel.subscribe(() => close(false));
    });
  }
}
