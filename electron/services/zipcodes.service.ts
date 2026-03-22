import { BaseRepository } from '../db/base.repository';
import { BaseService } from './base.service';
import type { Zipcode, IResult } from '../models';

const zipcodeRepo = new BaseRepository<Zipcode>('Zipcodes', 'ZipcodeID');

class ZipcodesService extends BaseService<Zipcode> {
  constructor() {
    super(zipcodeRepo);
  }

  async getCities(): Promise<IResult<string[]>> {
    try {
      const rows = await zipcodeRepo.query(
        `SELECT DISTINCT [City] FROM [Zipcodes] WHERE [IsDisplay] = 1 ORDER BY [City]`,
      );
      return { success: true, data: rows.map((r) => r.City) };
    } catch (e: any) {
      return { success: false, message: e.message, error: e.message };
    }
  }

  async getAreasByCity(city: string): Promise<IResult<Zipcode[]>> {
    try {
      const data = await zipcodeRepo.query(
        `SELECT * FROM [Zipcodes] WHERE [City] = @city AND [IsDisplay] = 1 ORDER BY [Zipcode]`,
        { city },
      );
      return { success: true, data };
    } catch (e: any) {
      return { success: false, message: e.message, error: e.message };
    }
  }
}

export const zipcodesService = new ZipcodesService();
