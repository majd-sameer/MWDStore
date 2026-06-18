/*
 * Public API Surface of core
 *
 * Shared security layer for the storefront and admin apps: in-memory access
 * token + silent refresh, functional interceptors (auth / error / correlation
 * id / base URL), XSRF, and functional route guards. App-specific values are
 * supplied via `provideCore({ apiBaseUrl })`.
 */

// Config
export * from './lib/config/core-config';

// Auth
export * from './lib/auth/auth.service';
export * from './lib/auth/jwt';

// i18n (language + direction)
export * from './lib/i18n/language.service';
export * from './lib/i18n/money.pipe';

// Guards
export * from './lib/guards/auth.guard';
export * from './lib/guards/role.guard';

// Interceptors
export * from './lib/interceptors/correlation-id.interceptor';
export * from './lib/interceptors/accept-language.interceptor';
export * from './lib/interceptors/base-url.interceptor';
export * from './lib/interceptors/auth.interceptor';
export * from './lib/interceptors/error.interceptor';

// Setup
export * from './lib/provide-core';
