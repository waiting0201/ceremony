import { BaseRepository } from '../db/base.repository';
import { BaseService } from './base.service';
import type { Signup, IResult } from '../models';
import { getPool } from '../db/connection';

const signupRepo = new BaseRepository<Signup>('Signups', 'SignupID');

export interface SignupSearchParams {
  year?: number;
  ceremonyCategoryId?: string;
  signupType?: number;
  keyword?: string;
  dateFrom?: string;
  dateTo?: string;
  page?: number;
  pageSize?: number;
}

class SignupsService extends BaseService<Signup> {
  constructor() {
    super(signupRepo);
  }

  async search(params: SignupSearchParams): Promise<IResult<{ rows: any[]; total: number }>> {
    try {
      const pool = await getPool();
      const conditions: string[] = ['1=1'];
      const inputs: Record<string, any> = {};

      if (params.year) {
        conditions.push('sv.[Year] = @year');
        inputs['year'] = params.year;
      }
      if (params.ceremonyCategoryId) {
        conditions.push('sv.[CeremonyCategoryID] = @ccId');
        inputs['ccId'] = params.ceremonyCategoryId;
      }
      if (params.signupType) {
        conditions.push('sv.[SignupType] = @stype');
        inputs['stype'] = params.signupType;
      }
      if (params.keyword) {
        conditions.push(
          `(sv.[Name] LIKE @kw OR sv.[HallName] LIKE @kw OR sv.[Phone] LIKE @kw OR sv.[DeadNameOne] LIKE @kw OR sv.[LivingNameOne] LIKE @kw)`,
        );
        inputs['kw'] = `%${params.keyword}%`;
      }
      if (params.dateFrom) {
        conditions.push('s.[CeremonyDate] >= @dateFrom');
        inputs['dateFrom'] = params.dateFrom;
      }
      if (params.dateTo) {
        conditions.push('s.[CeremonyDate] <= @dateTo');
        inputs['dateTo'] = params.dateTo;
      }

      const where = conditions.join(' AND ');
      const page = params.page || 1;
      const pageSize = params.pageSize || 20;
      const offset = (page - 1) * pageSize;

      // Count
      const countReq = pool.request();
      for (const [k, v] of Object.entries(inputs)) countReq.input(k, v);
      const countResult = await countReq.query(
        `SELECT COUNT(*) AS total FROM [SignupView] sv
         LEFT JOIN [Signups] s ON sv.[SignupID] = s.[SignupID]
         WHERE ${where}`,
      );
      const total = countResult.recordset[0].total;

      // Data
      const dataReq = pool.request();
      for (const [k, v] of Object.entries(inputs)) dataReq.input(k, v);
      dataReq.input('offset', offset);
      dataReq.input('pageSize', pageSize);
      const dataResult = await dataReq.query(
        `SELECT sv.* FROM [SignupView] sv
         LEFT JOIN [Signups] s ON sv.[SignupID] = s.[SignupID]
         WHERE ${where}
         ORDER BY sv.[Year] DESC, sv.[CeremonySort], sv.[Number]
         OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY`,
      );

      return { success: true, data: { rows: dataResult.recordset, total } };
    } catch (e: any) {
      return { success: false, message: e.message, error: e.message };
    }
  }

  async getNextNumber(
    year: number,
    ceremonyCategoryId: string,
    signupType: number,
  ): Promise<IResult<number>> {
    try {
      const rows = await signupRepo.query(
        `SELECT ISNULL(MAX([Number]), 0) + 1 AS NextNumber
         FROM [Signups]
         WHERE [Year] = @year AND [CeremonyCategoryID] = @ccId AND [SignupType] = @stype`,
        { year, ccId: ceremonyCategoryId, stype: signupType },
      );
      return { success: true, data: (rows[0] as any).NextNumber };
    } catch (e: any) {
      return { success: false, message: e.message, error: e.message };
    }
  }
}

export const signupsService = new SignupsService();
