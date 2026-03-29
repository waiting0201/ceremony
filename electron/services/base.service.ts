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
      const msg = this.translateError(e);
      return { success: false, message: msg, error: e.message };
    }
  }

  async update(id: string | number, entity: Partial<T>): Promise<IResult<T>> {
    try {
      const data = await this.repo.update(id, entity);
      if (!data) return { success: false, message: '找不到資料' };
      return { success: true, data };
    } catch (e: any) {
      const msg = this.translateError(e);
      return { success: false, message: msg, error: e.message };
    }
  }

  async remove(id: string | number): Promise<IResult> {
    try {
      const ok = await this.repo.delete(id);
      if (!ok) return { success: false, message: '找不到資料' };
      return { success: true };
    } catch (e: any) {
      const msg = this.translateError(e);
      return { success: false, message: msg, error: e.message };
    }
  }

  protected translateError(e: any): string {
    const msg: string = e.message || '';
    // FK constraint violation (SQL Server error 547)
    if (e.number === 547 || msg.includes('REFERENCE constraint') || msg.includes('FOREIGN KEY')) {
      return '無法刪除，此資料仍被其他記錄參照使用中';
    }
    // Unique constraint violation (SQL Server error 2627/2601)
    if (e.number === 2627 || e.number === 2601 || msg.includes('UNIQUE') || msg.includes('duplicate key')) {
      return '資料重複，已存在相同的記錄';
    }
    return msg;
  }
}
