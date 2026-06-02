import { ComponentPortal } from '@angular/cdk/portal';
import { Overlay } from '@angular/cdk/overlay';
import { inject, Injectable } from '@angular/core';
import { ConfirmDialogComponent } from './confirm-dialog.component';
import type { ConfirmDialogConfig } from './confirm-dialog.types';

@Injectable({ providedIn: 'root' })
export class ConfirmDialogService {
  private readonly overlay = inject(Overlay);

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
