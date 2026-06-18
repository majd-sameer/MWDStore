/**
 * Production environment. `apiBaseUrl` is empty so requests stay same-origin
 * (the app is expected to be served behind a reverse proxy that forwards
 * `/api` to Store.Api) — this keeps the httpOnly refresh cookie and Angular's
 * XSRF protection working without CORS. Never put secrets here; this file is
 * bundled into the client.
 */
export const environment = {
  production: true,
  apiBaseUrl: '',
};
