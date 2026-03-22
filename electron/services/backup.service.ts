import { getPool } from '../db/connection';
import type { IResult } from '../models';

export async function backupDatabase(backupPath: string): Promise<IResult<string>> {
  try {
    const pool = await getPool();
    const dbName = process.env['DB_DATABASE'] || 'Ceremony';
    await pool.request().query(
      `BACKUP DATABASE [${dbName}] TO DISK = '${backupPath.replace(/'/g, "''")}' WITH FORMAT, INIT, NAME = '${dbName} Backup'`,
    );
    return { success: true, data: backupPath };
  } catch (e: any) {
    return { success: false, message: e.message, error: e.message };
  }
}
