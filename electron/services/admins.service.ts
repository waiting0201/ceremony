import { BaseRepository } from '../db/base.repository';
import { BaseService } from './base.service';
import type { Admin } from '../models';

const adminRepo = new BaseRepository<Admin>('Admins', 'AdminID');

export class AdminsService extends BaseService<Admin> {
  constructor() {
    super(adminRepo);
  }
}

export const adminsService = new AdminsService();
