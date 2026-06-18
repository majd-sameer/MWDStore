import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import type { Observable } from 'rxjs';
import { API_ROOT } from './http-utils';
import type { AuthResponse, LoginRequest, RegisterRequest } from './models';

/**
 * Auth endpoints (the Postman "Auth" folder). These only call the API and
 * return the `AuthResponse`; persisting / attaching the access token is the
 * responsibility of `core`, not this framework-pure data layer.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);

  /** POST /api/auth/register */
  register(body: RegisterRequest): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${API_ROOT}/auth/register`, body);
  }

  /** POST /api/auth/login */
  login(body: LoginRequest): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${API_ROOT}/auth/login`, body);
  }
}
