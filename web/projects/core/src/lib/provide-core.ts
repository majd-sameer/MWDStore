import {
  provideHttpClient,
  withFetch,
  withInterceptors,
  withXsrfConfiguration,
} from '@angular/common/http';
import { type EnvironmentProviders, makeEnvironmentProviders } from '@angular/core';
import {
  CORE_CONFIG,
  CORE_CONFIG_DEFAULTS,
  type CoreConfig,
} from './config/core-config';
import { acceptLanguageInterceptor } from './interceptors/accept-language.interceptor';
import { authInterceptor } from './interceptors/auth.interceptor';
import { baseUrlInterceptor } from './interceptors/base-url.interceptor';
import { correlationIdInterceptor } from './interceptors/correlation-id.interceptor';
import { errorInterceptor } from './interceptors/error.interceptor';

/**
 * One-call setup for the shared security layer. Each app invokes this in its
 * `app.config.ts`, passing its environment:
 *
 * ```ts
 * providers: [provideCore({ apiBaseUrl: environment.apiBaseUrl })]
 * ```
 *
 * It wires `HttpClient` with (in order) the correlation-id, base-URL, bearer
 * and error interceptors, plus Angular's built-in XSRF protection
 * (`withXsrfConfiguration`) so `HttpClient` reads the `XSRF-TOKEN` cookie and
 * echoes it back in `X-XSRF-TOKEN` on mutating requests. `withFetch()` is used
 * for clean SSR. Apps must NOT also call `provideHttpClient`.
 */
export function provideCore(config: CoreConfig = {}): EnvironmentProviders {
  const resolved = { ...CORE_CONFIG_DEFAULTS, ...config };

  return makeEnvironmentProviders([
    { provide: CORE_CONFIG, useValue: resolved },
    provideHttpClient(
      withFetch(),
      withInterceptors([
        correlationIdInterceptor,
        acceptLanguageInterceptor,
        baseUrlInterceptor,
        authInterceptor,
        errorInterceptor,
      ]),
      withXsrfConfiguration({
        cookieName: resolved.xsrfCookieName,
        headerName: resolved.xsrfHeaderName,
      }),
    ),
  ]);
}
