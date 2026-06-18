import { httpResource } from '@angular/common/http';
import { inject, Injectable, Injector, runInInjectionContext } from '@angular/core';
import {
  API_ROOT,
  catalogQueryParams,
  type CatalogProductQuery,
} from './http-utils';
import { LocaleState } from './locale-state';
import type {
  BrandDto,
  CategoryDto,
  ProductDetailModel,
  ProductListResult,
} from './models';

/**
 * Storefront catalog reads. Every endpoint here is a GET, so each is exposed as
 * a reactive `httpResource` factory: pass signals (or any reactive getter) and
 * the resource refetches when they change.
 *
 * @example
 * private readonly catalog = inject(CatalogService);
 * readonly query = signal<CatalogProductQuery>({ pageSize: 12 });
 * readonly products = this.catalog.productsResource(this.query);
 * // template: products.value()?.products, products.isLoading(), products.error()
 */
@Injectable({ providedIn: 'root' })
export class CatalogService {
  private readonly injector = inject(Injector);
  private readonly locale = inject(LocaleState);

  /** GET /api/catalog/products */
  productsResource(query: () => CatalogProductQuery = () => ({})) {
    return runInInjectionContext(this.injector, () =>
      httpResource<ProductListResult>(() => ({
        url: `${API_ROOT}/catalog/products`,
        params: { ...catalogQueryParams(query()), culture: this.locale.language() },
      })),
    );
  }

  /** GET /api/catalog/categories/{categoryId}/products */
  categoryProductsResource(
    categoryId: () => number,
    query: () => CatalogProductQuery = () => ({}),
  ) {
    return runInInjectionContext(this.injector, () =>
      httpResource<ProductListResult>(() => ({
        url: `${API_ROOT}/catalog/categories/${categoryId()}/products`,
        params: { ...catalogQueryParams(query()), culture: this.locale.language() },
      })),
    );
  }

  /** GET /api/catalog/products/{id} */
  productResource(id: () => number) {
    return runInInjectionContext(this.injector, () =>
      httpResource<ProductDetailModel>(() => ({
        url: `${API_ROOT}/catalog/products/${id()}`,
        params: { culture: this.locale.language() },
      })),
    );
  }

  /** GET /api/catalog/categories */
  categoriesResource() {
    return runInInjectionContext(this.injector, () =>
      httpResource<CategoryDto[]>(() => `${API_ROOT}/catalog/categories`),
    );
  }

  /** GET /api/catalog/brands */
  brandsResource() {
    return runInInjectionContext(this.injector, () =>
      httpResource<BrandDto[]>(() => `${API_ROOT}/catalog/brands`),
    );
  }

  /** GET /api/catalog/vendors/count — active vendor (centers) count for the home hero. */
  vendorCountResource() {
    return runInInjectionContext(this.injector, () =>
      httpResource<number>(() => `${API_ROOT}/catalog/vendors/count`),
    );
  }
}
