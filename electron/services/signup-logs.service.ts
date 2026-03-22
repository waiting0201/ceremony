import { BaseRepository } from '../db/base.repository';
import { BaseService } from './base.service';
import type { SignupLog, IResult } from '../models';
import { getPool } from '../db/connection';

const logRepo = new BaseRepository<SignupLog>('SignupLogs', 'SignupLogID');

class SignupLogsService extends BaseService<SignupLog> {
  constructor() {
    super(logRepo);
  }

  async search(params: {
    signupId?: string;
    keyword?: string;
    page?: number;
    pageSize?: number;
  }): Promise<IResult<{ rows: SignupLog[]; total: number }>> {
    try {
      const pool = await getPool();
      const conditions: string[] = ['1=1'];
      const inputs: Record<string, any> = {};

      if (params.signupId) {
        conditions.push('[SignupID] = @signupId');
        inputs['signupId'] = params.signupId;
      }
      if (params.keyword) {
        conditions.push('([Name] LIKE @kw OR [Admin] LIKE @kw)');
        inputs['kw'] = `%${params.keyword}%`;
      }

      const where = conditions.join(' AND ');
      const page = params.page || 1;
      const pageSize = params.pageSize || 20;
      const offset = (page - 1) * pageSize;

      const countReq = pool.request();
      for (const [k, v] of Object.entries(inputs)) countReq.input(k, v);
      const countResult = await countReq.query(
        `SELECT COUNT(*) AS total FROM [SignupLogs] WHERE ${where}`,
      );

      const dataReq = pool.request();
      for (const [k, v] of Object.entries(inputs)) dataReq.input(k, v);
      dataReq.input('offset', offset);
      dataReq.input('pageSize', pageSize);
      const dataResult = await dataReq.query(
        `SELECT * FROM [SignupLogs] WHERE ${where}
         ORDER BY [Createdate] DESC
         OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY`,
      );

      return {
        success: true,
        data: { rows: dataResult.recordset, total: countResult.recordset[0].total },
      };
    } catch (e: any) {
      return { success: false, message: e.message, error: e.message };
    }
  }
}

export const signupLogsService = new SignupLogsService();
