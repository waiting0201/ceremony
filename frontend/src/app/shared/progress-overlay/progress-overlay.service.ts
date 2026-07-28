import { Overlay } from '@angular/cdk/overlay';
import { ComponentPortal } from '@angular/cdk/portal';
import { inject, Injectable } from '@angular/core';
import { ProgressOverlayComponent } from './progress-overlay.component';
import type { ProgressOverlayConfig } from './progress-overlay.types';

export interface ProgressOverlayHandle {
  /** 更新顯示內容（會與目前 config 合併） */
  update(patch: Partial<ProgressOverlayConfig>): void;
  /** 使用者按下取消（或 Esc）時 resolve；overlay 不會自己關閉 */
  readonly canceled: Promise<void>;
  close(): void;
}

@Injectable({ providedIn: 'root' })
export class ProgressOverlayService {
  private readonly overlay = inject(Overlay);

  open(config: ProgressOverlayConfig): ProgressOverlayHandle {
    const overlayRef = this.overlay.create({
      hasBackdrop: false,
      scrollStrategy: this.overlay.scrollStrategies.block(),
      positionStrategy: this.overlay.position().global(),
    });
    const ref = overlayRef.attach(new ComponentPortal(ProgressOverlayComponent));

    let current: ProgressOverlayConfig = { cancelable: true, ...config };
    ref.setInput('config', current);

    let resolveCanceled!: () => void;
    const canceled = new Promise<void>((resolve) => (resolveCanceled = resolve));

    const update = (patch: Partial<ProgressOverlayConfig>): void => {
      // 必須換成「新物件」，否則 OnPush 不會偵測到變更
      current = { ...current, ...patch };
      ref.setInput('config', current);
    };

    ref.instance.cancel.subscribe(() => {
      if (current.cancelable === false) return;
      // 刻意不關閉：先切成「取消中」，等呼叫端真的把後端 job 停掉才 close()，
      // 避免畫面關了但伺服器還在燒 CPU。
      update({ cancelable: false, note: '取消中…' });
      resolveCanceled();
    });

    return {
      update,
      canceled,
      close: () => overlayRef.dispose(),
    };
  }
}
