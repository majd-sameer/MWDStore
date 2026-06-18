import { HttpClient, httpResource } from '@angular/common/http';
import { inject, Injectable, Injector, runInInjectionContext } from '@angular/core';
import type { Observable } from 'rxjs';
import { API_ROOT } from './http-utils';
import type { AccountProfile, UpdateProfileRequest } from './models';

/** Authenticated account profile: a GET read and a PUT command. */
@Injectable({ providedIn: 'root' })
export class AccountService {
  private readonly http = inject(HttpClient);
  private readonly injector = inject(Injector);

  /**
   * GET /api/account/me. Pass `enabled` to gate the request (e.g. only when the
   * user is authenticated) — the resource stays idle while it returns `false`.
   */
  profileResource(enabled: () => boolean = () => true) {
    return runInInjectionContext(this.injector, () =>
      httpResource<AccountProfile>(() =>
        enabled() ? `${API_ROOT}/account/me` : undefined,
      ),
    );
  }

  /** PUT /api/account/me */
  updateProfile(body: UpdateProfileRequest): Observable<AccountProfile> {
    return this.http.put<AccountProfile>(`${API_ROOT}/account/me`, body);
  }
}
