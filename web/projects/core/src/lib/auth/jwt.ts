/**
 * Minimal, dependency-free JWT helpers. We never trust these claims for
 * authorization decisions on the server — they're only used client-side to
 * shape the UI (show/hide admin links, route guards). The API re-validates the
 * signed token on every call.
 */
export interface JwtClaims {
  exp?: number;
  iat?: number;
  sub?: string;
  [claim: string]: unknown;
}

/** Claim types ASP.NET Core may use to carry roles. */
const ROLE_CLAIM_TYPES = [
  'role',
  'roles',
  'http://schemas.microsoft.com/ws/2008/06/identity/claims/role',
] as const;

function base64UrlDecode(segment: string): string | null {
  if (typeof atob !== 'function') {
    return null;
  }
  const base64 = segment.replace(/-/g, '+').replace(/_/g, '/');
  const padded = base64.padEnd(base64.length + ((4 - (base64.length % 4)) % 4), '=');
  const binary = atob(padded);
  // Re-decode as UTF-8 so non-ASCII claims (names, emails) survive.
  const utf8 = Array.prototype.map
    .call(binary, (char: string) => `%${`00${char.charCodeAt(0).toString(16)}`.slice(-2)}`)
    .join('');
  return decodeURIComponent(utf8);
}

/** Decodes a JWT payload, or returns `null` if the token is malformed. */
export function decodeJwt(token: string): JwtClaims | null {
  const parts = token.split('.');
  if (parts.length !== 3) {
    return null;
  }
  try {
    const json = base64UrlDecode(parts[1]);
    return json ? (JSON.parse(json) as JwtClaims) : null;
  } catch {
    return null;
  }
}

/** Extracts role strings from claims, handling single-value and array forms. */
export function extractRoles(claims: JwtClaims): string[] {
  for (const claimType of ROLE_CLAIM_TYPES) {
    const value = claims[claimType];
    if (typeof value === 'string') {
      return [value];
    }
    if (Array.isArray(value)) {
      return value.filter((entry): entry is string => typeof entry === 'string');
    }
  }
  return [];
}

/** True when `exp` has passed (optionally with a clock-skew allowance). */
export function isJwtExpired(claims: JwtClaims, skewSeconds = 0): boolean {
  if (typeof claims.exp !== 'number') {
    return false;
  }
  return Date.now() >= (claims.exp - skewSeconds) * 1000;
}
