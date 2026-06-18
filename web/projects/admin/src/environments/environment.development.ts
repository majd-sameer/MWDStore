/**
 * Development environment. `apiBaseUrl` is empty so requests stay relative and
 * are forwarded to Store.Api (https://localhost:7142) by the dev proxy
 * (`proxy.conf.json`), keeping everything same-origin.
 */
export const environment = {
  production: false,
  apiBaseUrl: '',
};
