import { HttpClient, httpResource } from '@angular/common/http';
import { inject, Injectable, Injector, runInInjectionContext } from '@angular/core';
import type { Observable } from 'rxjs';
import { API_ROOT } from './http-utils';
import type { CountryLookupDto, StateOrProvinceLookupDto } from './models';

/**
 * Public country/state lookups for storefront address forms (`/api/locations`).
 * Anonymous and read-only; only shipping-enabled countries are returned.
 */
@Injectable({ providedIn: 'root' })
export class LocationsService {
  private readonly http = inject(HttpClient);
  private readonly injector = inject(Injector);

  /** GET /api/locations/countries */
  countriesResource() {
    return runInInjectionContext(this.injector, () =>
      httpResource<CountryLookupDto[]>(() => `${API_ROOT}/locations/countries`),
    );
  }

  /**
   * GET /api/locations/countries/{countryId}/states
   *
   * `withRatesOnly` limits the list to states an enabled shipping provider has a
   * table-rate row for (used by the checkout so only shippable governorates show).
   */
  states(countryId: string, withRatesOnly = false): Observable<StateOrProvinceLookupDto[]> {
    return this.http.get<StateOrProvinceLookupDto[]>(
      `${API_ROOT}/locations/countries/${encodeURIComponent(countryId)}/states`,
      { params: withRatesOnly ? { withRatesOnly: true } : {} },
    );
  }
}
