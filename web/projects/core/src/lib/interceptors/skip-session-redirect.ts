import { HttpContextToken } from '@angular/common/http';

/**
 * When set to `true` on a request's `HttpContext`, the error interceptor will
 * NOT redirect to the login route if that request fails with 401 / a dead
 * session — it just clears the in-memory session and rethrows.
 *
 * Used by the storefront's silent boot session-restore: a logged-out visitor
 * has no refresh cookie, so the restore call legitimately 401s, and a guest
 * must stay on the page (and shop with a local cart) rather than be bounced to
 * login.
 */
export const SKIP_SESSION_REDIRECT = new HttpContextToken<boolean>(() => false);
