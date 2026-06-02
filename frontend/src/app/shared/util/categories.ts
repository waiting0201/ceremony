import type { CategoryNode } from '../../core/api/categories/category.models';

export interface FlatCategory {
  id: string;
  title: string;
  rootTitle: string;
  isRoot: boolean;
  depth: number;
}

/**
 * Flatten the two-level tree into a list suitable for dropdowns,
 * with breadcrumb-style titles (e.g. "春季 / 元宵法會").
 */
export function flattenCategories(tree: CategoryNode[]): FlatCategory[] {
  const out: FlatCategory[] = [];
  for (const root of tree) {
    out.push({
      id: root.id,
      title: root.title,
      rootTitle: root.title,
      isRoot: true,
      depth: 0,
    });
    for (const child of root.children) {
      out.push({
        id: child.id,
        title: `${root.title} / ${child.title}`,
        rootTitle: root.title,
        isRoot: false,
        depth: 1,
      });
    }
  }
  return out;
}

export function findCategoryTitle(tree: CategoryNode[], id: string | null): string | null {
  if (!id) return null;
  for (const root of tree) {
    if (root.id === id) return root.title;
    for (const child of root.children) {
      if (child.id === id) return `${root.title} / ${child.title}`;
    }
  }
  return null;
}
