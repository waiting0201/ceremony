import type { CategoryNode } from '../../core/api/categories/category.models';

/** 三個固定 root 法會 GUID（見 docs/glossary.md，跨年共用）。 */
export const SEASON_ROOT_IDS = {
  spring:    '18927907-dcad-42b2-8f2a-635c2e0fa98d', // 春季
  zhongyuan: '0c478f0e-787c-448e-ba7b-b1579f3f1fce', // 中元
  autumn:    '3864e4dc-24db-4544-acb3-3351592f6dab', // 秋季
} as const;

export const SEASON_ROOT_TITLES = {
  spring: '春季',
  zhongyuan: '中元',
  autumn: '秋季',
} as const;

export type CeremonySeason = keyof typeof SEASON_ROOT_IDS;

/** 月份對季別：1-4月→春季、5-8月→中元、9-12月→秋季（見 docs/business-rules-implicit.md）。 */
export function seasonForMonth(month: number): CeremonySeason {
  if (month <= 4) return 'spring';
  if (month <= 8) return 'zhongyuan';
  return 'autumn';
}

export function currentSeason(now: Date = new Date()): CeremonySeason {
  return seasonForMonth(now.getMonth() + 1);
}

/** 從載入的分類樹解析季別 root id：先比對固定 GUID，找不到再以 title 退而求其次。 */
export function resolveSeasonRootId(tree: CategoryNode[], season: CeremonySeason): string | null {
  const byId = tree.find((r) => r.id === SEASON_ROOT_IDS[season]);
  if (byId) return byId.id;
  return tree.find((r) => r.title === SEASON_ROOT_TITLES[season])?.id ?? null;
}
