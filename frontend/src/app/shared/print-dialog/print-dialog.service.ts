import { ComponentPortal } from '@angular/cdk/portal';
import { Overlay } from '@angular/cdk/overlay';
import { inject, Injectable } from '@angular/core';
import { PrintDialogComponent } from './print-dialog.component';
import type { PrintDialogConfig, PrintDialogResult } from './print-dialog.types';

@Injectable({ providedIn: 'root' })
export class PrintDialogService {
  private readonly overlay = inject(Overlay);

  /** @returns 使用者的選擇；取消時回 `null`。 */
  ask(config: PrintDialogConfig): Promise<PrintDialogResult | null> {
    return new Promise((resolve) => {
      const overlayRef = this.overlay.create({
        hasBackdrop: false,
        scrollStrategy: this.overlay.scrollStrategies.block(),
        positionStrategy: this.overlay.position().global(),
      });
      const ref = overlayRef.attach(new ComponentPortal(PrintDialogComponent));
      ref.setInput('config', config);

      const close = (result: PrintDialogResult | null) => {
        overlayRef.dispose();
        resolve(result);
      };

      ref.instance.print.subscribe((r) => close(r));
      ref.instance.cancel.subscribe(() => close(null));
    });
  }
}
