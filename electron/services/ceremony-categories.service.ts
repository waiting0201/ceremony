import { BaseRepository } from '../db/base.repository';
import { BaseService } from './base.service';
import type { CeremonyCategory, IResult } from '../models';

const repo = new BaseRepository<CeremonyCategory>('CeremonyCategorys', 'CeremonyCategoryID');

export interface CategoryTreeNode extends CeremonyCategory {
  children: CategoryTreeNode[];
}

class CeremonyCategorysService extends BaseService<CeremonyCategory> {
  constructor() {
    super(repo);
  }

  async getTree(): Promise<IResult<CategoryTreeNode[]>> {
    try {
      const all = await repo.query(
        `SELECT * FROM [CeremonyCategorys] ORDER BY [Sort], [Title]`,
      );
      const tree = this.buildTree(all, null);
      return { success: true, data: tree };
    } catch (e: any) {
      return { success: false, message: e.message, error: e.message };
    }
  }

  async getNextSort(parentId: string | null): Promise<IResult<number>> {
    try {
      const where = parentId
        ? `[ParentID] = @parentId`
        : `[ParentID] IS NULL`;
      const params = parentId ? { parentId } : {};
      const rows = await repo.query(
        `SELECT ISNULL(MAX([Sort]), 0) + 1 AS NextSort FROM [CeremonyCategorys] WHERE ${where}`,
        params,
      );
      return { success: true, data: (rows[0] as any).NextSort };
    } catch (e: any) {
      return { success: false, message: e.message, error: e.message };
    }
  }

  private buildTree(items: CeremonyCategory[], parentId: string | null): CategoryTreeNode[] {
    return items
      .filter((i) => i.ParentID === parentId)
      .map((i) => ({
        ...i,
        children: this.buildTree(items, i.CeremonyCategoryID),
      }));
  }
}

export const ceremonyCategorysService = new CeremonyCategorysService();
