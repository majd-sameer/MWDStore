import { InjectionToken } from '@angular/core';


export interface CoreConfig {

  apiBaseUrl?: string;
  ssrApiBaseUrl?: string;
  loginUrl?: string;
  forbiddenUrl?: string;
  refreshPath?: string;
  logoutPath?: string;
  correlationIdHeader?: string;
  xsrfCookieName?: string;
  xsrfHeaderName?: string;
}

export type ResolvedCoreConfig = Required<CoreConfig>;

export const CORE_CONFIG = new InjectionToken<ResolvedCoreConfig>('CORE_CONFIG');

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
