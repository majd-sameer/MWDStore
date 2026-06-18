import { InjectionToken } from '@angular/core';

/**
 * Application-supplied configuration for the `core` security layer. Each app
 * passes its environment values into {@link provideCore}; nothing here is a
 * secret — only public, build-time origins and route names.
 */
export interface CoreConfig {
  /**
   * Backend origin prepended to root-relative `/api/...` requests by the
   * base-URL interceptor. Leave empty (the default) to keep requests
   * same-origin and rely on a dev proxy / reverse proxy — this keeps Angular's
   * built-in XSRF and the httpOnly refresh cookie working without CORS.
   */
  apiBaseUrl?: string;
  /**
   * Absolute backend origin used to resolve relative `/api/...` requests
   * **only during SSR** (the server `fetch` can't resolve a relative URL).
   * Browser requests still use {@link apiBaseUrl}, so XSRF and cookies stay
   * same-origin. Falls back to {@link apiBaseUrl} when empty.
   */
  ssrApiBaseUrl?: string;
  /** Route to redirect unauthenticated users to. */
  loginUrl?: string;
  /** Route to redirect users lacking a required role to. */
  forbiddenUrl?: string;
  /** Path of the cookie-authenticated silent-refresh endpoint. */
  refreshPath?: string;
  /** Path of the logout endpoint (revokes the refresh cookie server-side). */
  logoutPath?: string;
  /** Header used to correlate a request across client and server logs. */
  correlationIdHeader?: string;
  /** Cookie Angular reads the XSRF token from. */
  xsrfCookieName?: string;
  /** Header Angular sends the XSRF token in. */
  xsrfHeaderName?: string;
}

/** {@link CoreConfig} with every field resolved to a concrete value. */
export type ResolvedCoreConfig = Required<CoreConfig>;

/** DI token holding the fully-resolved {@link CoreConfig}. */
export const CORE_CONFIG = new InjectionToken<ResolvedCoreConfig>('CORE_CONFIG');

/** Defaults assuming a same-origin (proxied) API. */
export const CORE_CONFIG_DEFAULTS: ResolvedCoreConfig = {
  apiBaseUrl: '',
  ssrApiBaseUrl: '',
  loginUrl: '/login',
  forbiddenUrl: '/forbidden',
  refreshPath: '/api/auth/refresh',
  logoutPath: '/api/auth/logout',
  correlationIdHeader: 'X-Correlation-Id',
  xsrfCookieName: 'XSRF-TOKEN',
  xsrfHeaderName: 'X-XSRF-TOKEN',
};
