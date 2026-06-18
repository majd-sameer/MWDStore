import { HttpClient, httpResource } from '@angular/common/http';
import { inject, Injectable, Injector, runInInjectionContext } from '@angular/core';
import type { Observable } from 'rxjs';
import { API_ROOT } from '../http-utils';
import type { CountryLookupDto, StateOrProvinceLookupDto } from '../models';

/** Read-only country/state lookups for admin forms (`/api/admin/locations`). */
@Injectable({ providedIn: 'root' })
export class AdminLocationsService {
  private readonly http = inject(HttpClient);
  private readonly injector = inject(Injector);

  /** GET /api/admin/locations/countries */
  countriesResource() {
    return runInInjectionContext(this.injector, () =>
      httpResource<CountryLookupDto[]>(
        () => `${API_ROOT}/admin/locations/countries`,
      ),
    );
  }

  /** GET /api/admin/locations/countries/{countryId}/states */
  states(countryId: string): Observable<StateOrProvinceLookupDto[]> {
    return this.http.get<StateOrProvinceLookupDto[]>(
      `${API_ROOT}/admin/locations/countries/${encodeURIComponent(countryId)}/states`,
    );
  }
}
