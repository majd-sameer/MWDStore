import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import type { Observable } from 'rxjs';
import { API_ROOT } from './http-utils';
import type {
  AuthResponse,
  ForgotPasswordRequest,
  LoginRequest,
  MfaDisableRequest,
  MfaEnableRequest,
  MfaEnableResponse,
  MfaSetupResponse,
  MfaStatusResponse,
  MfaVerifyRequest,
  RegisterRequest,
  ResetPasswordRequest,
} from './models';

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

  /** POST /api/auth/forgot-password — always resolves 200, regardless of whether the account exists. */
  forgotPassword(body: ForgotPasswordRequest): Observable<void> {
    return this.http.post<void>(`${API_ROOT}/auth/forgot-password`, body);
  }

  /** POST /api/auth/reset-password */
  resetPassword(body: ResetPasswordRequest): Observable<void> {
    return this.http.post<void>(`${API_ROOT}/auth/reset-password`, body);
  }

  // ----- MFA -----------------------------------------------------------------

  /** POST /api/auth/mfa/verify — exchanges a login challenge + TOTP/recovery code for tokens. */
  mfaVerify(body: MfaVerifyRequest): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${API_ROOT}/auth/mfa/verify`, body);
  }

  /** GET /api/account/mfa/status (authenticated). */
  mfaStatus(): Observable<MfaStatusResponse> {
    return this.http.get<MfaStatusResponse>(`${API_ROOT}/account/mfa/status`);
  }

  /** POST /api/account/mfa/setup — (re)generates the authenticator key for enrollment. */
  mfaSetup(): Observable<MfaSetupResponse> {
    return this.http.post<MfaSetupResponse>(`${API_ROOT}/account/mfa/setup`, null);
  }

  /** POST /api/account/mfa/enable — confirms a code and returns recovery codes. */
  mfaEnable(body: MfaEnableRequest): Observable<MfaEnableResponse> {
    return this.http.post<MfaEnableResponse>(`${API_ROOT}/account/mfa/enable`, body);
  }

  /** POST /api/account/mfa/disable — requires a current authenticator or recovery code. */
  mfaDisable(body: MfaDisableRequest): Observable<void> {
    return this.http.post<void>(`${API_ROOT}/account/mfa/disable`, body);
  }
}
