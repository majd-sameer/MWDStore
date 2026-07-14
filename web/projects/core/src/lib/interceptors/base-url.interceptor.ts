import type { HttpInterceptorFn } from '@angular/common/http';
import { inject, PLATFORM_ID } from '@angular/core';
import { isPlatformServer } from '@angular/common';
import { CORE_CONFIG } from '../config/core-config';


export const baseUrlInterceptor: HttpInterceptorFn = (req, next) => {
  const { apiBaseUrl, ssrApiBaseUrl } = inject(CORE_CONFIG);
  const onServer = isPlatformServer(inject(PLATFORM_ID));
  const base = onServer ? ssrApiBaseUrl || apiBaseUrl : apiBaseUrl;

  if (base && req.url.startsWith('/')) {
    return next(req.clone({ url: `${base}${req.url}` }));
  }
  return next(req);
};
