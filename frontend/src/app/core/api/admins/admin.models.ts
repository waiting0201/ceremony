export interface AdminListItem {
  id: number;
  username: string;
  name: string | null;
}

export interface AdminListResponse {
  items: AdminListItem[];
  total: number;
}

export interface CreateAdminRequest {
  username: string;
  password: string;
  name?: string | null;
}

/**
 * 更新管理者請求。username 不可變更。
 * password 為 null/undefined 視為「不變更密碼」；提供值則需通過後端驗證。
 */
export interface UpdateAdminRequest {
  password?: string | null;
  name?: string | null;
}
