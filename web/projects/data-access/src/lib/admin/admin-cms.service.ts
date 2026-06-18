import { HttpClient, httpResource } from '@angular/common/http';
import { inject, Injectable, Injector, runInInjectionContext } from '@angular/core';
import type { Observable } from 'rxjs';
import { API_ROOT } from '../http-utils';
import type {
  AdminMenuDto,
  AdminMenuItemDto,
  AdminNewsCategoryDto,
  AdminNewsItemDetail,
  AdminNewsItemListItem,
  AdminPageDto,
  MenuItemUpsertRequest,
  MenuUpsertRequest,
  NewsCategoryUpsertRequest,
  NewsItemUpsertRequest,
  PageUpsertRequest,
} from '../models';

/** Admin CMS (pages + menus) and news management. */
@Injectable({ providedIn: 'root' })
export class AdminCmsService {
  private readonly http = inject(HttpClient);
  private readonly injector = inject(Injector);

  // ----- Pages ---------------------------------------------------------------

  /** GET /api/admin/pages */
  pagesResource() {
    return runInInjectionContext(this.injector, () =>
      httpResource<AdminPageDto[]>(() => `${API_ROOT}/admin/pages`),
    );
  }

  createPage(body: PageUpsertRequest): Observable<AdminPageDto> {
    return this.http.post<AdminPageDto>(`${API_ROOT}/admin/pages`, body);
  }

  updatePage(id: number, body: PageUpsertRequest): Observable<AdminPageDto> {
    return this.http.put<AdminPageDto>(`${API_ROOT}/admin/pages/${id}`, body);
  }

  deletePage(id: number): Observable<void> {
    return this.http.delete<void>(`${API_ROOT}/admin/pages/${id}`);
  }

  // ----- Menus ---------------------------------------------------------------

  /** GET /api/admin/menus */
  menusResource() {
    return runInInjectionContext(this.injector, () =>
      httpResource<AdminMenuDto[]>(() => `${API_ROOT}/admin/menus`),
    );
  }

  createMenu(body: MenuUpsertRequest): Observable<AdminMenuDto> {
    return this.http.post<AdminMenuDto>(`${API_ROOT}/admin/menus`, body);
  }

  updateMenu(id: number, body: MenuUpsertRequest): Observable<AdminMenuDto> {
    return this.http.put<AdminMenuDto>(`${API_ROOT}/admin/menus/${id}`, body);
  }

  deleteMenu(id: number): Observable<void> {
    return this.http.delete<void>(`${API_ROOT}/admin/menus/${id}`);
  }

  addMenuItem(menuId: number, body: MenuItemUpsertRequest): Observable<AdminMenuItemDto> {
    return this.http.post<AdminMenuItemDto>(
      `${API_ROOT}/admin/menus/${menuId}/items`,
      body,
    );
  }

  updateMenuItem(
    menuId: number,
    itemId: number,
    body: MenuItemUpsertRequest,
  ): Observable<AdminMenuItemDto> {
    return this.http.put<AdminMenuItemDto>(
      `${API_ROOT}/admin/menus/${menuId}/items/${itemId}`,
      body,
    );
  }

  deleteMenuItem(menuId: number, itemId: number): Observable<void> {
    return this.http.delete<void>(
      `${API_ROOT}/admin/menus/${menuId}/items/${itemId}`,
    );
  }

  // ----- News ----------------------------------------------------------------

  /** GET /api/admin/news/categories */
  newsCategoriesResource() {
    return runInInjectionContext(this.injector, () =>
      httpResource<AdminNewsCategoryDto[]>(
        () => `${API_ROOT}/admin/news/categories`,
      ),
    );
  }

  createNewsCategory(body: NewsCategoryUpsertRequest): Observable<AdminNewsCategoryDto> {
    return this.http.post<AdminNewsCategoryDto>(
      `${API_ROOT}/admin/news/categories`,
      body,
    );
  }

  updateNewsCategory(
    id: number,
    body: NewsCategoryUpsertRequest,
  ): Observable<AdminNewsCategoryDto> {
    return this.http.put<AdminNewsCategoryDto>(
      `${API_ROOT}/admin/news/categories/${id}`,
      body,
    );
  }

  deleteNewsCategory(id: number): Observable<void> {
    return this.http.delete<void>(`${API_ROOT}/admin/news/categories/${id}`);
  }

  /** GET /api/admin/news/items */
  newsItemsResource() {
    return runInInjectionContext(this.injector, () =>
      httpResource<AdminNewsItemListItem[]>(() => `${API_ROOT}/admin/news/items`),
    );
  }

  getNewsItem(id: number): Observable<AdminNewsItemDetail> {
    return this.http.get<AdminNewsItemDetail>(`${API_ROOT}/admin/news/items/${id}`);
  }

  createNewsItem(body: NewsItemUpsertRequest): Observable<AdminNewsItemDetail> {
    return this.http.post<AdminNewsItemDetail>(`${API_ROOT}/admin/news/items`, body);
  }

  updateNewsItem(id: number, body: NewsItemUpsertRequest): Observable<AdminNewsItemDetail> {
    return this.http.put<AdminNewsItemDetail>(
      `${API_ROOT}/admin/news/items/${id}`,
      body,
    );
  }

  deleteNewsItem(id: number): Observable<void> {
    return this.http.delete<void>(`${API_ROOT}/admin/news/items/${id}`);
  }
}
