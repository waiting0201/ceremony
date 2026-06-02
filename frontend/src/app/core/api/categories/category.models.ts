export interface CategoryNode {
  id: string;
  title: string;
  sort: number;
  children: CategoryNode[];
}

export interface CategoryListResponse {
  items: CategoryNode[];
  total: number;
}

export interface CategoryItem {
  id: string;
  title: string;
  sort: number;
  parentId: string | null;
}

export interface CreateCategoryRequest {
  title: string;
  sort: number;
  parentId?: string | null;
}

export interface UpdateCategoryRequest {
  title: string;
  sort: number;
}
