import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../../environments/environment';
import type {
  CategoryItem,
  CategoryListResponse,
  CreateCategoryRequest,
  UpdateCategoryRequest,
} from './category.models';

@Injectable({ providedIn: 'root' })
export class CategoryApi {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/categories`;

  list(): Promise<CategoryListResponse> {
    return firstValueFrom(this.http.get<CategoryListResponse>(this.base));
  }

  create(body: CreateCategoryRequest): Promise<CategoryItem> {
    return firstValueFrom(this.http.post<CategoryItem>(this.base, body));
  }

  update(id: string, body: UpdateCategoryRequest): Promise<CategoryItem> {
    return firstValueFrom(this.http.put<CategoryItem>(`${this.base}/${id}`, body));
  }

  remove(id: string): Promise<void> {
    return firstValueFrom(this.http.delete<void>(`${this.base}/${id}`));
  }
}
