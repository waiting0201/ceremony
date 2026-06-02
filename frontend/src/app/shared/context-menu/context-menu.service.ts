import { ComponentPortal } from '@angular/cdk/portal';
import {
  Overlay,
  OverlayConfig,
  OverlayRef,
  type ConnectedPosition,
} from '@angular/cdk/overlay';
import { inject, Injectable } from '@angular/core';
import { ContextMenuPanelComponent } from './context-menu-panel.component';
import {
  resolveItems,
  type ContextMenuItem,
  type ContextMenuOpenConfig,
  type ResolvedItem,
} from './context-menu.types';

@Injectable({ providedIn: 'root' })
export class ContextMenuService {
  private readonly overlay = inject(Overlay);

  private currentRef: OverlayRef | null = null;

  open<T>(config: ContextMenuOpenConfig<T>): void {
    this.close();
    const resolved = resolveItems(config.items, config.context);

    const overlayConfig: OverlayConfig = {
      hasBackdrop: true,
      backdropClass: 'ctx-menu-backdrop',
      scrollStrategy: this.overlay.scrollStrategies.close(),
      positionStrategy: this.buildPositionStrategy(config.origin),
      disposeOnNavigation: true,
    };

    const overlayRef = this.overlay.create(overlayConfig);
    this.currentRef = overlayRef;

    const portal = new ComponentPortal(ContextMenuPanelComponent);
    const componentRef = overlayRef.attach(portal);
    componentRef.setInput('items', resolved as ResolvedItem<unknown>[]);

    const closeWith = (entry?: ResolvedItem<T>) => {
      if (this.currentRef !== overlayRef) return;
      overlayRef.dispose();
      this.currentRef = null;
      if (entry && entry.enabled && !entry.item.divider) {
        void entry.item.onClick(config.context);
      }
    };

    componentRef.instance.select.subscribe((entry) => closeWith(entry as ResolvedItem<T>));
    componentRef.instance.dismiss.subscribe(() => closeWith());
    overlayRef.backdropClick().subscribe(() => closeWith());
    overlayRef.detachments().subscribe(() => {
      if (this.currentRef === overlayRef) this.currentRef = null;
    });

    queueMicrotask(() => componentRef.instance.focus());
  }

  close(): void {
    if (this.currentRef) {
      this.currentRef.dispose();
      this.currentRef = null;
    }
  }

  private buildPositionStrategy(origin: ContextMenuOpenConfig<unknown>['origin']) {
    if (origin instanceof HTMLElement) {
      const positions: ConnectedPosition[] = [
        { originX: 'end', originY: 'bottom', overlayX: 'end', overlayY: 'top' },
        { originX: 'end', originY: 'top', overlayX: 'end', overlayY: 'bottom' },
        { originX: 'start', originY: 'bottom', overlayX: 'start', overlayY: 'top' },
      ];
      return this.overlay
        .position()
        .flexibleConnectedTo(origin)
        .withPositions(positions)
        .withPush(true);
    }
    return this.overlay
      .position()
      .flexibleConnectedTo({
        x: origin.x,
        y: origin.y,
      })
      .withPositions([
        { originX: 'start', originY: 'top', overlayX: 'start', overlayY: 'top' },
        { originX: 'start', originY: 'bottom', overlayX: 'start', overlayY: 'bottom' },
        { originX: 'end', originY: 'top', overlayX: 'end', overlayY: 'top' },
      ])
      .withPush(true);
  }
}

export type { ContextMenuItem };
