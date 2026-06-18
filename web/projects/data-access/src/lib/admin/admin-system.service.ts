import { HttpClient, httpResource } from '@angular/common/http';
import { inject, Injectable, Injector, runInInjectionContext } from '@angular/core';
import type { Observable } from 'rxjs';
import { API_ROOT, toQueryParams } from '../http-utils';
import type {
  AdminCountryDto,
  AdminResourceDto,
  AppSettingDto,
  AppSettingUpdateRequest,
  CountryUpsertRequest,
  CultureDto,
  ResourceUpsertRequest,
  StateOrProvinceLookupDto,
  StateOrProvinceUpsertRequest,
} from '../models';

/** Admin system pages: app settings, country/state CRUD, localization resources. */
@Injectable({ providedIn: 'root' })
export class AdminSystemService {
  private readonly http = inject(HttpClient);
  private readonly injector = inject(Injector);

  // ----- Settings -------------------------------------------------------------

  /** GET /api/admin/settings */
  settingsResource() {
    return runInInjectionContext(this.injector, () =>
      httpResource<AppSettingDto[]>(() => `${API_ROOT}/admin/settings`),
    );
  }

  /** PUT /api/admin/settings */
  updateSettings(body: AppSettingUpdateRequest): Observable<void> {
    return this.http.put<void>(`${API_ROOT}/admin/settings`, body);
  }

  // ----- Countries / states ------------------------------------------------------

  /** GET /api/admin/locations/countries/detail */
  countriesResource() {
    return runInInjectionContext(this.injector, () =>
      httpResource<AdminCountryDto[]>(
        () => `${API_ROOT}/admin/locations/countries/detail`,
      ),
    );
  }

  createCountry(body: CountryUpsertRequest): Observable<AdminCountryDto> {
    return this.http.post<AdminCountryDto>(
      `${API_ROOT}/admin/locations/countries`,
      body,
    );
  }

  updateCountry(id: string, body: CountryUpsertRequest): Observable<AdminCountryDto> {
    return this.http.put<AdminCountryDto>(
      `${API_ROOT}/admin/locations/countries/${encodeURIComponent(id)}`,
      body,
    );
  }

  deleteCountry(id: string): Observable<void> {
    return this.http.delete<void>(
      `${API_ROOT}/admin/locations/countries/${encodeURIComponent(id)}`,
    );
  }

  states(countryId: string): Observable<StateOrProvinceLookupDto[]> {
    return this.http.get<StateOrProvinceLookupDto[]>(
      `${API_ROOT}/admin/locations/countries/${encodeURIComponent(countryId)}/states`,
    );
  }

  createState(
    countryId: string,
    body: StateOrProvinceUpsertRequest,
  ): Observable<StateOrProvinceLookupDto> {
    return this.http.post<StateOrProvinceLookupDto>(
      `${API_ROOT}/admin/locations/countries/${encodeURIComponent(countryId)}/states`,
      body,
    );
  }

  updateState(
    id: number,
    body: StateOrProvinceUpsertRequest,
  ): Observable<StateOrProvinceLookupDto> {
    return this.http.put<StateOrProvinceLookupDto>(
      `${API_ROOT}/admin/locations/states/${id}`,
      body,
    );
  }

  deleteState(id: number): Observable<void> {
    return this.http.delete<void>(`${API_ROOT}/admin/locations/states/${id}`);
  }

  // ----- Localization -------------------------------------------------------------

  /** GET /api/admin/localization/cultures */
  culturesResource() {
    return runInInjectionContext(this.injector, () =>
      httpResource<CultureDto[]>(() => `${API_ROOT}/admin/localization/cultures`),
    );
  }

  createCulture(body: CultureDto): Observable<CultureDto> {
    return this.http.post<CultureDto>(
      `${API_ROOT}/admin/localization/cultures`,
      body,
    );
  }

  resources(cultureId: string, query?: string): Observable<AdminResourceDto[]> {
    return this.http.get<AdminResourceDto[]>(
      `${API_ROOT}/admin/localization/resources`,
      { params: toQueryParams({ cultureId, query }) },
    );
  }

  upsertResource(body: ResourceUpsertRequest): Observable<AdminResourceDto> {
    return this.http.post<AdminResourceDto>(
      `${API_ROOT}/admin/localization/resources`,
      body,
    );
  }

  deleteResource(id: number): Observable<void> {
    return this.http.delete<void>(`${API_ROOT}/admin/localization/resources/${id}`);
  }
}
