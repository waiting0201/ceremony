import { BaseRepository } from '../db/base.repository';
import { BaseService } from './base.service';
import type { Believer, IResult } from '../models';
import { getPool } from '../db/connection';

const believerRepo = new BaseRepository<Believer>('Believers', 'BelieverID');

export interface BelieverSearchParams {
  keyword?: string;
  page?: number;
  pageSize?: number;
}

class BelieversService extends BaseService<Believer> {
  constructor() {
    super(believerRepo);
  }

  async search(params: BelieverSearchParams): Promise<IResult<{ rows: any[]; total: number }>> {
    try {
      const pool = await getPool();
      const page = params.page || 1;
      const pageSize = params.pageSize || 20;
      const offset = (page - 1) * pageSize;

      let where = '1=1';
      const request = pool.request();

      if (params.keyword) {
        where += ` AND (b.[Name] LIKE @kw OR b.[Phone] LIKE @kw OR b.[HallName] LIKE @kw)`;
        request.input('kw', `%${params.keyword}%`);
      }

      request.input('offset', offset);
      request.input('pageSize', pageSize);

      const countResult = await pool
        .request()
        .input('kw', params.keyword ? `%${params.keyword}%` : '%')
        .query(
          `SELECT COUNT(*) AS total FROM [Believers] b WHERE ${where}`,
        );
      const total = countResult.recordset[0].total;

      const dataResult = await request.query(
        `SELECT b.*,
                mz.[City] AS MailCity, mz.[Area] AS MailArea,
                tz.[City] AS TextCity, tz.[Area] AS TextArea
         FROM [Believers] b
         LEFT JOIN [Zipcodes] mz ON b.[MailZipcodeID] = mz.[ZipcodeID]
         LEFT JOIN [Zipcodes] tz ON b.[TextZipcodeID] = tz.[ZipcodeID]
         WHERE ${where}
         ORDER BY b.[Name]
         OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY`,
      );

      return { success: true, data: { rows: dataResult.recordset, total } };
    } catch (e: any) {
      return { success: false, message: e.message, error: e.message };
    }
  }

  async lookup(keyword: string): Promise<IResult<Believer[]>> {
    try {
      const data = await believerRepo.query(
        `SELECT TOP 10 * FROM [Believers]
         WHERE [Name] LIKE @kw OR [Phone] LIKE @kw OR [HallName] LIKE @kw
         ORDER BY [Name]`,
        { kw: `%${keyword}%` },
      );
      return { success: true, data };
    } catch (e: any) {
      return { success: false, message: e.message, error: e.message };
    }
  }
}

export const believersService = new BelieversService();
