import type { HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { inject } from '@angular/core';
import { CORE_CONFIG } from '../config/core-config';
import { AuthService } from '../auth/auth.service';

function isApiRequest(req: HttpRequest<unknown>, apiBaseUrl: string): boolean {
  if (req.url.startsWith('/api')) {
    return true;
  }
  return apiBaseUrl !== '' && req.url.startsWith(`${apiBaseUrl}/api`);
}


export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const { apiBaseUrl } = inject(CORE_CONFIG);
  const token = auth.accessToken();

  if (token && isApiRequest(req, apiBaseUrl)) {
    return next(req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }));
  }
  return next(req);
};
