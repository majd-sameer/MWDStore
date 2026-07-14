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
