import { BaseRepository } from '../db/base.repository';
import type { IResult } from '../models';

export class BaseService<T extends Record<string, any>> {
  constructor(protected readonly repo: BaseRepository<T>) {}

  async getAll(): Promise<IResult<T[]>> {
    try {
      const data = await this.repo.getAll();
      return { success: true, data };
    } catch (e: any) {
      return { success: false, message: e.message, error: e.message };
    }
  }

  async getById(id: string | number): Promise<IResult<T>> {
    try {
      const data = await this.repo.getById(id);
      if (!data) return { success: false, message: '找不到資料' };
      return { success: true, data };
    } catch (e: any) {
      return { success: false, message: e.message, error: e.message };
    }
  }

  async create(entity: Partial<T>): Promise<IResult<T>> {
    try {
      const data = await this.repo.insert(entity);
      return { success: true, data };
    } catch (e: any) {
      return { success: false, message: e.message, error: e.message };
    }
  }

  async update(id: string | number, entity: Partial<T>): Promise<IResult<T>> {
    try {
      const data = await this.repo.update(id, entity);
      if (!data) return { success: false, message: '找不到資料' };
      return { success: true, data };
    } catch (e: any) {
      return { success: false, message: e.message, error: e.message };
    }
  }

  async remove(id: string | number): Promise<IResult> {
    try {
      const ok = await this.repo.delete(id);
      if (!ok) return { success: false, message: '找不到資料' };
      return { success: true };
    } catch (e: any) {
      return { success: false, message: e.message, error: e.message };
    }
  }
}
