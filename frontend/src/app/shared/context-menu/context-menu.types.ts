import type { IconName } from '../icon/icon.component';

export interface ContextMenuItem<T> {
  id: string;
  label: string;
  icon?: IconName;
  danger?: boolean;
  divider?: boolean;
  enabledWhen?: (ctx: T) => boolean | { enabled: false; reason: string };
  onClick: (ctx: T) => void | Promise<void>;
}

export interface ContextMenuOpenConfig<T> {
  origin: { x: number; y: number } | HTMLElement;
  items: ContextMenuItem<T>[];
  context: T;
}

export interface ResolvedItem<T> {
  item: ContextMenuItem<T>;
  enabled: boolean;
  disabledReason: string | null;
}

export function resolveItems<T>(
  items: ContextMenuItem<T>[],
  context: T,
): ResolvedItem<T>[] {
  return items.map((item) => {
    if (!item.enabledWhen) return { item, enabled: true, disabledReason: null };
    const result = item.enabledWhen(context);
    if (typeof result === 'boolean') {
      return { item, enabled: result, disabledReason: null };
    }
    return { item, enabled: false, disabledReason: result.reason };
  });
}
