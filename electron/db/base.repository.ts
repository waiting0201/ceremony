import { getPool } from './connection';
import * as sql from 'mssql';
import type { IResult } from '../models';

export class BaseRepository<T extends Record<string, any>> {
  constructor(
    protected readonly tableName: string,
    protected readonly primaryKey: string = `${tableName.replace(/s$/, '')}ID`,
  ) {}

  async getAll(): Promise<T[]> {
    const pool = await getPool();
    const result = await pool.request().query(`SELECT * FROM [${this.tableName}]`);
    return result.recordset as T[];
  }

  async getById(id: string | number): Promise<T | null> {
    const pool = await getPool();
    const result = await pool
      .request()
      .input('id', id)
      .query(`SELECT * FROM [${this.tableName}] WHERE [${this.primaryKey}] = @id`);
    return (result.recordset[0] as T) || null;
  }

  async insert(entity: Partial<T>): Promise<T> {
    const pool = await getPool();
    const columns = Object.keys(entity);
    const values = columns.map((c) => `@${c}`);
    const request = pool.request();
    for (const col of columns) {
      request.input(col, (entity as any)[col]);
    }
    const result = await request.query(
      `INSERT INTO [${this.tableName}] (${columns.map((c) => `[${c}]`).join(', ')})
       OUTPUT INSERTED.*
       VALUES (${values.join(', ')})`,
    );
    return result.recordset[0] as T;
  }

  async update(id: string | number, entity: Partial<T>): Promise<T | null> {
    const pool = await getPool();
    const columns = Object.keys(entity).filter((c) => c !== this.primaryKey);
    if (columns.length === 0) return this.getById(id);

    const setClauses = columns.map((c) => `[${c}] = @${c}`);
    const request = pool.request().input('id', id);
    for (const col of columns) {
      request.input(col, (entity as any)[col]);
    }
    const result = await request.query(
      `UPDATE [${this.tableName}]
       SET ${setClauses.join(', ')}
       OUTPUT INSERTED.*
       WHERE [${this.primaryKey}] = @id`,
    );
    return (result.recordset[0] as T) || null;
  }

  async delete(id: string | number): Promise<boolean> {
    const pool = await getPool();
    const result = await pool
      .request()
      .input('id', id)
      .query(`DELETE FROM [${this.tableName}] WHERE [${this.primaryKey}] = @id`);
    return (result.rowsAffected[0] ?? 0) > 0;
  }

  async query(sqlText: string, params?: Record<string, any>): Promise<T[]> {
    const pool = await getPool();
    const request = pool.request();
    if (params) {
      for (const [key, val] of Object.entries(params)) {
        request.input(key, val);
      }
    }
    const result = await request.query(sqlText);
    return result.recordset as T[];
  }

  async count(where?: string, params?: Record<string, any>): Promise<number> {
    const pool = await getPool();
    const request = pool.request();
    if (params) {
      for (const [key, val] of Object.entries(params)) {
        request.input(key, val);
      }
    }
    const sql = where
      ? `SELECT COUNT(*) AS cnt FROM [${this.tableName}] WHERE ${where}`
      : `SELECT COUNT(*) AS cnt FROM [${this.tableName}]`;
    const result = await request.query(sql);
    return result.recordset[0].cnt;
  }
}
