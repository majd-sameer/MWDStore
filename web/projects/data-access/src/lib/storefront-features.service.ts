import { HttpClient, httpResource } from '@angular/common/http';
import { inject, Injectable, Injector, runInInjectionContext } from '@angular/core';
import type { Observable } from 'rxjs';
import { API_ROOT, toQueryParams } from './http-utils';
import { LocaleState } from './locale-state';
import type {
  AddWishListItemRequest,
  ComparisonProductDto,
  ContactAreaPublicDto,
  NewsDetailDto,
  NewsListItemDto,
  PublicPageDto,
  RecentlyViewedDto,
  ReviewDto,
  SubmitContactRequest,
  SubmitReviewRequest,
  WishListDto,
} from './models';

/**
 * Storefront features beyond the core buy flow: wishlist, product reviews,
 * CMS pages, news, contact, comparison and recently-viewed. Auth-only
 * endpoints (wishlist/comparison/recently-viewed) require a signed-in user.
 */
@Injectable({ providedIn: 'root' })
export class StorefrontFeaturesService {
  private readonly http = inject(HttpClient);
  private readonly injector = inject(Injector);
  private readonly locale = inject(LocaleState);

  // ----- Wishlist (auth) -------------------------------------------------------

  wishlist(): Observable<WishListDto> {
    return this.http.get<WishListDto>(`${API_ROOT}/wishlist`);
  }

  addToWishlist(body: AddWishListItemRequest): Observable<WishListDto> {
    return this.http.post<WishListDto>(`${API_ROOT}/wishlist/items`, body);
  }

  removeFromWishlist(itemId: number): Observable<void> {
    return this.http.delete<void>(`${API_ROOT}/wishlist/items/${itemId}`);
  }

  // ----- Reviews ----------------------------------------------------------------

  reviews(productId: number, page = 1): Observable<ReviewDto[]> {
    return this.http.get<ReviewDto[]>(
      `${API_ROOT}/products/${productId}/reviews`,
      { params: toQueryParams({ page }) },
    );
  }

  submitReview(productId: number, body: SubmitReviewRequest): Observable<ReviewDto> {
    return this.http.post<ReviewDto>(`${API_ROOT}/products/${productId}/reviews`, body);
  }

  // ----- Content ------------------------------------------------------------------

  page(slug: string): Observable<PublicPageDto> {
    return this.http.get<PublicPageDto>(`${API_ROOT}/pages/${encodeURIComponent(slug)}`);
  }

  news(page = 1): Observable<NewsListItemDto[]> {
    return this.http.get<NewsListItemDto[]>(`${API_ROOT}/news`, {
      params: toQueryParams({ page }),
    });
  }

  /** GET /api/news as a reactive resource (SSR-rendered + transfer-cached). */
  newsResource(page: () => number = () => 1) {
    return runInInjectionContext(this.injector, () =>
      httpResource<NewsListItemDto[]>(() => ({
        url: `${API_ROOT}/news`,
        params: toQueryParams({ page: page(), culture: this.locale.language() }),
      })),
    );
  }

  newsDetail(slug: string): Observable<NewsDetailDto> {
    return this.http.get<NewsDetailDto>(`${API_ROOT}/news/${encodeURIComponent(slug)}`);
  }

  contactAreas(): Observable<ContactAreaPublicDto[]> {
    return this.http.get<ContactAreaPublicDto[]>(`${API_ROOT}/contact/areas`);
  }

  submitContact(body: SubmitContactRequest): Observable<void> {
    return this.http.post<void>(`${API_ROOT}/contact`, body);
  }

  // ----- Comparison (auth) ----------------------------------------------------------

  comparison(): Observable<ComparisonProductDto[]> {
    return this.http.get<ComparisonProductDto[]>(`${API_ROOT}/comparison`);
  }

  addToComparison(productId: number): Observable<void> {
    return this.http.post<void>(`${API_ROOT}/comparison`, { productId });
  }

  removeFromComparison(productId: number): Observable<void> {
    return this.http.delete<void>(`${API_ROOT}/comparison/${productId}`);
  }

  // ----- Recently viewed (auth) ------------------------------------------------------

  recentlyViewed(count = 8): Observable<RecentlyViewedDto[]> {
    return this.http.get<RecentlyViewedDto[]>(`${API_ROOT}/recently-viewed`, {
      params: toQueryParams({ count }),
    });
  }

  recordView(productId: number): Observable<void> {
    return this.http.post<void>(`${API_ROOT}/recently-viewed`, { productId });
  }
}
