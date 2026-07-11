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

/**
 * Owns the authenticated session for both apps.
 *
 * Security posture:
 * - The **access token lives only in memory** (a signal) — never in
 *   `localStorage`/`sessionStorage`, so it isn't readable by injected scripts
 *   and is gone on tab close. A page refresh recovers it via silent refresh.
 * - The **refresh token never touches JS** — it's an httpOnly, Secure,
 *   SameSite=strict cookie set by the API. `refresh()` / `logout()` send it
 *   with `withCredentials` and the API rotates it on every use.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly api = inject(AuthApi);
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly config = inject(CORE_CONFIG);

  private readonly _accessToken = signal<string | null>(null);
  private readonly _session = signal<AuthResponse | null>(null);

  /** Current in-memory access token (read-only). */
  readonly accessToken = this._accessToken.asReadonly();
  /** Last `AuthResponse` (userId/email/fullName), read-only. */
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

  /** Single in-flight refresh shared across concurrent 401s. */
  private refreshInFlight: Observable<string> | null = null;

  /** Single in-flight boot restore shared by the app initializer and guards. */
  private restoreInFlight: Observable<string> | null = null;

  private readonly _sessionRestored = signal(false);
  /**
   * `true` once the silent boot restore has settled (succeeded or failed). Until
   * then, route guards must wait rather than treat the user as a guest — a hard
   * refresh drops the in-memory token, so the guard would otherwise bounce a
   * still-authenticated user to login. See {@link ensureSessionRestored}.
   */
  readonly sessionRestored = this._sessionRestored.asReadonly();

  hasRole(role: string): boolean {
    return this.roles().includes(role);
  }

  hasAnyRole(roles: readonly string[]): boolean {
    const current = this.roles();
    return roles.some((role) => current.includes(role));
  }

  /**
   * Authenticates and stores the resulting access token in memory. When the
   * account has MFA enabled the API returns `{ mfaRequired, challengeToken }`
   * instead of tokens — no session is stored and the caller must complete the
   * challenge via {@link mfaVerify}.
   */
  login(body: LoginRequest): Observable<AuthResponse> {
    return this.api.login(body).pipe(
      tap((response) => {
        if (!response.mfaRequired) {
          this.setSession(response);
        }
      }),
    );
  }

  /** Completes an MFA login challenge and stores the resulting session. */
  mfaVerify(challengeToken: string, code: string): Observable<AuthResponse> {
    return this.api
      .mfaVerify({ challengeToken, code })
      .pipe(tap((response) => this.setSession(response)));
  }

  /** Registers and stores the resulting access token in memory. */
  register(body: RegisterRequest): Observable<AuthResponse> {
    return this.api.register(body).pipe(tap((response) => this.setSession(response)));
  }

  /**
   * Exchanges the httpOnly refresh cookie for a fresh access token (with
   * rotation server-side). Concurrent callers share one in-flight request.
   */
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

  /**
   * Silent boot session restore. Like {@link refresh} but never bounces to
   * login on failure (it sets {@link SKIP_SESSION_REDIRECT}), so a guest with no
   * refresh cookie simply stays logged out. Returns the access token on success.
   */
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

  /**
   * Resolves once the session is known: `true` if the user is authenticated
   * (either an in-memory token already, or a successful silent restore), `false`
   * for a genuine guest. Route guards call this so a hard refresh waits for the
   * refresh-cookie exchange instead of bouncing the user to login.
   */
  ensureSessionRestored(): Observable<boolean> {
    if (this.isAuthenticated() || this._sessionRestored()) {
      return of(this.isAuthenticated());
    }
    return this.restoreSession().pipe(
      map(() => this.isAuthenticated()),
      catchError(() => of(this.isAuthenticated())),
    );
  }

  /** Revokes the refresh cookie server-side, clears state, redirects to login. */
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

  /** Stores a new session (called after login/register/refresh). */
  setSession(response: AuthResponse): void {
    this._accessToken.set(response.accessToken);
    this._session.set(response);
  }

  /** Drops the in-memory token without contacting the server. */
  clearSession(): void {
    this._accessToken.set(null);
    this._session.set(null);
  }
}
