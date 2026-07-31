import { ComponentPortal } from '@angular/cdk/portal';
import { Overlay } from '@angular/cdk/overlay';
import { inject, Injectable } from '@angular/core';
import { PrintDialogComponent } from './print-dialog.component';
import type { PrintDialogConfig, PrintDialogRequest, PrintDialogResult } from './print-dialog.types';

@Injectable({ providedIn: 'root' })
export class PrintDialogService {
  private readonly overlay = inject(Overlay);

  /**
   * @returns 使用者的選擇；取消時回 `null`。
   *
   * previewBlob 的 object URL 在這裡建、在這裡回收：綁在 overlayRef 的生命週期上，
   * 才能保證取消 / 列印 / 例外三條路都成對 revoke（放元件裡就得靠 ngOnDestroy 賭時序）。
   */
  ask(request: PrintDialogRequest): Promise<PrintDialogResult | null> {
    return new Promise((resolve) => {
      const { previewBlob, ...rest } = request;
      const previewUrl = previewBlob ? URL.createObjectURL(previewBlob) : null;

      const overlayRef = this.overlay.create({
        hasBackdrop: false,
        scrollStrategy: this.overlay.scrollStrategies.block(),
        positionStrategy: this.overlay.position().global(),
      });
      const ref = overlayRef.attach(new ComponentPortal(PrintDialogComponent));
      ref.setInput('config', { ...rest, previewUrl } satisfies PrintDialogConfig);

      const close = (result: PrintDialogResult | null) => {
        // 先銷毀 overlay（iframe 跟著走）再 revoke，順序顛倒會讓 Chromium 讀到已釋放的 URL
        overlayRef.dispose();
        if (previewUrl) URL.revokeObjectURL(previewUrl);
        resolve(result);
      };

      ref.instance.print.subscribe((r) => close(r));
      ref.instance.cancel.subscribe(() => close(null));
    });
  }
}
