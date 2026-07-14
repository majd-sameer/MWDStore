import type { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { CORE_CONFIG } from '../config/core-config';

function newCorrelationId(): string {
  const cryptoObj = globalThis.crypto;
  if (cryptoObj?.randomUUID) {
    return cryptoObj.randomUUID();
  }
  return `${Date.now().toString(16)}-${Math.random().toString(16).slice(2)}`;
}


export const correlationIdInterceptor: HttpInterceptorFn = (req, next) => {
  const { correlationIdHeader } = inject(CORE_CONFIG);
  return next(
    req.clone({ setHeaders: { [correlationIdHeader]: newCorrelationId() } }),
  );
};
