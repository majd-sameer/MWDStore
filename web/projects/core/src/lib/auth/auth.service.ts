import { HttpClient, HttpContext } from '@angular/common/http';
import { computed, inject, Injectable, signal } from '@angular/core';
import { Router } from '@angular/router';
import {
  AuthService as AuthApi,
  type AuthResponse,
  type LoginRequest,
  type RegisterRequest,
} from 'data-access';
import { catchError, finalize, map, Observable, of, shareReplay, tap } from 'rxjs';
import { CORE_CONFIG } from '../config/core-config';
import { SKIP_SESSION_REDIRECT } from '../interceptors/skip-session-redirect';
import { decodeJwt, extractRoles } from './jwt';


@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly api = inject(AuthApi);
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly config = inject(CORE_CONFIG);

  private readonly _accessToken = signal<string | null>(null);
  private readonly _session = signal<AuthResponse | null>(null);

  readonly accessToken = this._accessToken.asReadonly();
  readonly session = this._session.asReadonly();

  private readonly claims = computed(() => {
    const token = this._accessToken();
    return token ? decodeJwt(token) : null;
  });

  readonly isAuthenticated = computed(() => this._accessToken() !== null);
  readonly roles = computed(() => {
    const claims = this.claims();
    return claims ? extractRoles(claims) : [];
  });
  readonly userId = computed(() => this._session()?.userId ?? null);
  readonly email = computed(() => this._session()?.email ?? null);
  readonly fullName = computed(() => this._session()?.fullName ?? null);

  private refreshInFlight: Observable<string> | null = null;

  private restoreInFlight: Observable<string> | null = null;

  private readonly _sessionRestored = signal(false);
  readonly sessionRestored = this._sessionRestored.asReadonly();

  hasRole(role: string): boolean {
    return this.roles().includes(role);
  }

  hasAnyRole(roles: readonly string[]): boolean {
    const current = this.roles();
    return roles.some((role) => current.includes(role));
  }

  login(body: LoginRequest): Observable<AuthResponse> {
    return this.api.login(body).pipe(tap((response) => this.setSession(response)));
  }

  register(body: RegisterRequest): Observable<AuthResponse> {
    return this.api.register(body).pipe(tap((response) => this.setSession(response)));
  }


  refresh(): Observable<string> {
    if (this.refreshInFlight) {
      return this.refreshInFlight;
    }
    this.refreshInFlight = this.http
      .post<AuthResponse>(this.config.refreshPath, null, { withCredentials: true })
      .pipe(
        tap((response) => this.setSession(response)),
        map((response) => response.accessToken ?? ''),
        finalize(() => (this.refreshInFlight = null)),
        shareReplay(1),
      );
    return this.refreshInFlight;
  }


  restoreSession(): Observable<string> {
    if (this.restoreInFlight) {
      return this.restoreInFlight;
    }
    this.restoreInFlight = this.http
      .post<AuthResponse>(this.config.refreshPath, null, {
        withCredentials: true,
        context: new HttpContext().set(SKIP_SESSION_REDIRECT, true),
      })
      .pipe(
        tap((response) => this.setSession(response)),
        map((response) => response.accessToken ?? ''),
        finalize(() => {
          this.restoreInFlight = null;
          this._sessionRestored.set(true);
        }),
        shareReplay(1),
      );
    return this.restoreInFlight;
  }

  ensureSessionRestored(): Observable<boolean> {
    if (this.isAuthenticated() || this._sessionRestored()) {
      return of(this.isAuthenticated());
    }
    return this.restoreSession().pipe(
      map(() => this.isAuthenticated()),
      catchError(() => of(this.isAuthenticated())),
    );
  }

  logout(): void {
    this.http
      .post(this.config.logoutPath, null, { withCredentials: true })
      .pipe(
        finalize(() => {
          this.clearSession();
          void this.router.navigate([this.config.loginUrl]);
        }),
      )
      .subscribe({ error: () => undefined });
  }

  setSession(response: AuthResponse): void {
    this._accessToken.set(response.accessToken);
    this._session.set(response);
  }

  clearSession(): void {
    this._accessToken.set(null);
    this._session.set(null);
  }
}
