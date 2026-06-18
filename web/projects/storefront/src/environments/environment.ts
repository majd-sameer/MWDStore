/**
 * Production environment. `apiBaseUrl` is empty so browser requests stay
 * same-origin (the app is expected to be served behind a reverse proxy that
 * forwards `/api` to Store.Api) — this keeps the httpOnly refresh cookie and
 * Angular's XSRF protection working without CORS. Set `ssrApiBaseUrl` to the
 * API origin reachable from the SSR server (e.g. an internal service URL).
 * Never put secrets here; this file is bundled into the client.
 */
export const environment = {
  production: true,
  apiBaseUrl: '',
  // The SSR Node server and Store.Api run on the same host; the API is reachable
  // internally on :8080 (see DEPLOYMENT-RUNBOOK §2.2/§2.3). Baking it here is the
  // runbook's "permanent fix" so the built server bundle no longer needs the
  // manual ssrApiBaseUrl patch. `apiBaseUrl` stays empty (browser = same-origin).
  ssrApiBaseUrl: 'http://localhost:8080',
};
