import { ComponentPortal } from '@angular/cdk/portal';
import { Overlay } from '@angular/cdk/overlay';
import { inject, Injectable } from '@angular/core';
import type { ChunkedPrintRun } from '../../core/reports/chunked-print.service';
import { BatchPrintPanelComponent } from './batch-print-panel.component';

@Injectable({ providedIn: 'root' })
export class BatchPrintPanelService {
  private readonly overlay = inject(Overlay);

  /**
   * 顯示分段列印面板。
   * @returns 使用者按「關閉」時 resolve——刻意不在跑完時自動關：使用者要能對照實體紙張，
   *          發現某段沒印好就當場按重印。
   */
  open(run: ChunkedPrintRun): Promise<void> {
    return new Promise((resolve) => {
      const overlayRef = this.overlay.create({
        hasBackdrop: false,
        scrollStrategy: this.overlay.scrollStrategies.block(),
        positionStrategy: this.overlay.position().global(),
      });
      const ref = overlayRef.attach(new ComponentPortal(BatchPrintPanelComponent));
      ref.setInput('run', run);

      ref.instance.close.subscribe(() => {
        overlayRef.dispose();
        resolve();
      });
    });
  }
}
