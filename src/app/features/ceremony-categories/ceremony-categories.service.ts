import { Injectable, inject } from '@angular/core';
import { IpcService } from '../../core/services/ipc.service';
import type { IpcResult } from '../../core/models/ipc-result.model';

export interface CeremonyCategory {
  CeremonyCategoryID: string;
  Title: string;
  ParentID: string | null;
  Sort: number;
}

export interface CategoryTreeNode extends CeremonyCategory {
  children: CategoryTreeNode[];
}

@Injectable({ providedIn: 'root' })
export class CeremonyCategoriesService {
  private ipc = inject(IpcService);

  getTree(): Promise<IpcResult<CategoryTreeNode[]>> {
    return this.ipc.invoke<CategoryTreeNode[]>('ceremony-categories:tree');
  }

  create(data: Partial<CeremonyCategory>): Promise<IpcResult<CeremonyCategory>> {
    return this.ipc.invoke<CeremonyCategory>('ceremony-categories:create', data);
  }

  update(id: string, data: Partial<CeremonyCategory>): Promise<IpcResult<CeremonyCategory>> {
    return this.ipc.invoke<CeremonyCategory>('ceremony-categories:update', id, data);
  }

  delete(id: string): Promise<IpcResult> {
    return this.ipc.invoke('ceremony-categories:delete', id);
  }

  getNextSort(parentId: string | null): Promise<IpcResult<number>> {
    return this.ipc.invoke<number>('ceremony-categories:nextSort', parentId);
  }
}
