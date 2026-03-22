import { BaseRepository } from '../db/base.repository';
import type { Admin, IResult } from '../models';

const adminRepo = new BaseRepository<Admin>('Admins', 'AdminID');

let currentSession: { AdminID: number; Name: string; Username: string } | null = null;

export async function login(
  username: string,
  password: string,
): Promise<IResult<{ AdminID: number; Name: string; Username: string }>> {
  try {
    const admins = await adminRepo.query(
      `SELECT * FROM [Admins] WHERE [Username] = @username AND [Password] = @password AND [IsEnabled] = 1`,
      { username, password },
    );
    if (admins.length === 0) {
      return { success: false, message: '帳號或密碼錯誤，或帳號已停用' };
    }
    const admin = admins[0];
    currentSession = {
      AdminID: admin.AdminID,
      Name: admin.Name || admin.Username,
      Username: admin.Username,
    };
    return { success: true, data: currentSession };
  } catch (e: any) {
    return { success: false, message: e.message, error: e.message };
  }
}

export function logout(): IResult {
  currentSession = null;
  return { success: true };
}

export function getSession(): IResult<{
  AdminID: number;
  Name: string;
  Username: string;
} | null> {
  return { success: true, data: currentSession };
}
