import { HttpClient, httpResource } from '@angular/common/http';
import { inject, Injectable, Injector, runInInjectionContext } from '@angular/core';
import type { Observable } from 'rxjs';
import { API_ROOT, type PagedResult, toQueryParams } from '../http-utils';
import type {
  AdminActivityDto,
  AdminContactAreaDto,
  AdminContactDto,
  AdminProductTemplateDto,
  AdminSearchQueryDto,
  AdminShipmentDto,
  AdminVendorDto,
  ContactAreaUpsertRequest,
  ProductTemplateUpsertRequest,
  ShipmentCreateRequest,
  VendorUpsertRequest,
} from '../models';

/** Admin operations: shipments, vendors, contacts, system logs, product templates. */
@Injectable({ providedIn: 'root' })
export class AdminOperationsService {
  private readonly http = inject(HttpClient);
  private readonly injector = inject(Injector);

  // ----- Shipments -------------------------------------------------------------

  /** GET /api/admin/shipments?orderId= */
  shipments(orderId?: number): Observable<AdminShipmentDto[]> {
    return this.http.get<AdminShipmentDto[]>(`${API_ROOT}/admin/shipments`, {
      params: toQueryParams({ orderId }),
    });
  }

  /** POST /api/admin/shipments */
  createShipment(body: ShipmentCreateRequest): Observable<AdminShipmentDto> {
    return this.http.post<AdminShipmentDto>(`${API_ROOT}/admin/shipments`, body);
  }

  /** PUT /api/admin/shipments/{id}/tracking */
  updateTracking(id: number, trackingNumber: string | null): Observable<void> {
    return this.http.put<void>(
      `${API_ROOT}/admin/shipments/${id}/tracking`,
      JSON.stringify(trackingNumber),
      { headers: { 'Content-Type': 'application/json' } },
    );
  }

  // ----- Vendors ---------------------------------------------------------------

  /** GET /api/admin/vendors */
  vendorsResource() {
    return runInInjectionContext(this.injector, () =>
      httpResource<AdminVendorDto[]>(() => `${API_ROOT}/admin/vendors`),
    );
  }

  createVendor(body: VendorUpsertRequest): Observable<AdminVendorDto> {
    return this.http.post<AdminVendorDto>(`${API_ROOT}/admin/vendors`, body);
  }

  updateVendor(id: number, body: VendorUpsertRequest): Observable<AdminVendorDto> {
    return this.http.put<AdminVendorDto>(`${API_ROOT}/admin/vendors/${id}`, body);
  }

  deleteVendor(id: number): Observable<void> {
    return this.http.delete<void>(`${API_ROOT}/admin/vendors/${id}`);
  }

  // ----- Contacts --------------------------------------------------------------

  /** GET /api/admin/contacts */
  contactsResource() {
    return runInInjectionContext(this.injector, () =>
      httpResource<AdminContactDto[]>(() => `${API_ROOT}/admin/contacts`),
    );
  }

  deleteContact(id: number): Observable<void> {
    return this.http.delete<void>(`${API_ROOT}/admin/contacts/${id}`);
  }

  /** GET /api/admin/contacts/areas */
  contactAreasResource() {
    return runInInjectionContext(this.injector, () =>
      httpResource<AdminContactAreaDto[]>(() => `${API_ROOT}/admin/contacts/areas`),
    );
  }

  createContactArea(body: ContactAreaUpsertRequest): Observable<AdminContactAreaDto> {
    return this.http.post<AdminContactAreaDto>(
      `${API_ROOT}/admin/contacts/areas`,
      body,
    );
  }

  updateContactArea(
    id: number,
    body: ContactAreaUpsertRequest,
  ): Observable<AdminContactAreaDto> {
    return this.http.put<AdminContactAreaDto>(
      `${API_ROOT}/admin/contacts/areas/${id}`,
      body,
    );
  }

  deleteContactArea(id: number): Observable<void> {
    return this.http.delete<void>(`${API_ROOT}/admin/contacts/areas/${id}`);
  }

  // ----- System logs ------------------------------------------------------------

  /** GET /api/admin/logs/activities — paged envelope with total count. */
  activitiesResource(
    query: () => { page?: number; pageSize?: number } = () => ({}),
  ) {
    return runInInjectionContext(this.injector, () =>
      httpResource<PagedResult<AdminActivityDto>>(() => {
        const q = query();
        return {
          url: `${API_ROOT}/admin/logs/activities`,
          params: toQueryParams({ page: q.page, pageSize: q.pageSize }),
        };
      }),
    );
  }

  /** GET /api/admin/logs/search-queries */
  searchQueriesResource() {
    return runInInjectionContext(this.injector, () =>
      httpResource<AdminSearchQueryDto[]>(
        () => `${API_ROOT}/admin/logs/search-queries`,
      ),
    );
  }

  // ----- Product templates --------------------------------------------------------

  /** GET /api/admin/product-templates */
  templatesResource() {
    return runInInjectionContext(this.injector, () =>
      httpResource<AdminProductTemplateDto[]>(
        () => `${API_ROOT}/admin/product-templates`,
      ),
    );
  }

  createTemplate(body: ProductTemplateUpsertRequest): Observable<AdminProductTemplateDto> {
    return this.http.post<AdminProductTemplateDto>(
      `${API_ROOT}/admin/product-templates`,
      body,
    );
  }

  updateTemplate(
    id: number,
    body: ProductTemplateUpsertRequest,
  ): Observable<AdminProductTemplateDto> {
    return this.http.put<AdminProductTemplateDto>(
      `${API_ROOT}/admin/product-templates/${id}`,
      body,
    );
  }

  deleteTemplate(id: number): Observable<void> {
    return this.http.delete<void>(`${API_ROOT}/admin/product-templates/${id}`);
  }
}
