/**
 * Development environment.
 *
 * - `apiBaseUrl` is empty so **browser** requests stay relative and are
 *   forwarded to Store.Api by the dev proxy (`proxy.conf.json`), keeping
 *   everything same-origin (XSRF + cookies work).
 * - `ssrApiBaseUrl` points the **SSR server** at the backend's plain-HTTP
 *   endpoint (http://localhost:5094) so server-side `fetch` can resolve the
 *   relative `/api` calls without tripping on the self-signed HTTPS dev cert.
 */
export const environment = {
  production: false,
  apiBaseUrl: '',
  ssrApiBaseUrl: 'http://localhost:5094',
};
