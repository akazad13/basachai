import type { Problem } from './types';

/** The API as seen from the browser. Server-side code uses API_URL so it can call the API over a private address. */
export const PUBLIC_API = process.env.NEXT_PUBLIC_API_URL ?? 'http://localhost:5080';

export class ApiError extends Error {
  constructor(public readonly problem: Problem) {
    super(problem.title ?? problem.code);
  }
  get status() { return this.problem.status; }
  get code() { return this.problem.code; }
  /** Message keys per field, from a 400 validation_failed response. */
  get fields() { return this.problem.errors ?? {}; }
}

async function toError(res: Response): Promise<ApiError> {
  let body: Partial<Problem> = {};
  try { body = await res.json(); } catch { /* not JSON */ }
  return new ApiError({ status: res.status, code: body.code ?? 'error_generic', title: body.title, errors: body.errors, retryAfter: body.retryAfter });
}

/**
 * Server-side GET for public data. Never cached by Next.js: results must match the database (tests reseed it)
 * and each search records an analytics event. Edge caching is done by the CDN from the API's Cache-Control.
 */
export async function serverGet<T>(path: string, init: { headers?: Record<string, string> } = {}): Promise<T> {
  const base = process.env.API_URL ?? PUBLIC_API;
  const res = await fetch(base + path, { cache: 'no-store', headers: { Accept: 'application/json', ...init.headers } });
  if (!res.ok) throw await toError(res);
  return res.json() as Promise<T>;
}

function readCookie(name: string): string | undefined {
  if (typeof document === 'undefined') return undefined;
  return document.cookie.split('; ').find(c => c.startsWith(name + '='))?.slice(name.length + 1);
}

/** The visit id cookie: the web server reads it too, so searches it renders and reveals in the browser share one session. */
export const VISIT_COOKIE = 'bc_visit';

/** An id for this browser session (a cookie with no expiry) so analytics can group a visit's searches and reveals. */
export function sessionId(): string | undefined {
  try {
    let id = readCookie(VISIT_COOKIE);
    if (!id) {
      id = crypto.randomUUID();
      document.cookie = `${VISIT_COOKIE}=${id}; path=/; SameSite=Lax${location.protocol === 'https:' ? '; Secure' : ''}`;
    }
    return id;
  } catch { return undefined; }
}

/**
 * The antiforgery cookie lives only as long as the browser session, while sign-in lasts 30 days.
 * After a browser restart a signed-in visitor has no token, so fetch a fresh one.
 */
export async function ensureCsrf(force = false): Promise<void> {
  if (!force && readCookie('XSRF-TOKEN')) return;
  await fetch(PUBLIC_API + '/v1/auth/csrf', { credentials: 'include', headers: { Accept: 'application/json' } });
}

type Init = { method?: string; json?: unknown; body?: BodyInit; headers?: Record<string, string>; keepalive?: boolean };

/** Browser call with the session cookie, the antiforgery header and JSON in and out. */
export async function api<T = void>(path: string, init: Init = {}): Promise<T> {
  let res = await send(path, init);
  if (res.status === 403 && init.method && init.method !== 'GET') {
    // A missing or stale antiforgery token: get a new one and try once more.
    const problem = await res.clone().json().catch(() => null) as Partial<Problem> | null;
    if (problem?.code === 'csrf_failed') { await ensureCsrf(true); res = await send(path, init); }
  }
  if (!res.ok) throw await toError(res);
  if (res.status === 204 || res.headers.get('content-length') === '0') return undefined as T;
  const text = await res.text();
  return (text ? JSON.parse(text) : undefined) as T;
}

function send(path: string, init: Init): Promise<Response> {
  const headers: Record<string, string> = { Accept: 'application/json', ...init.headers };
  const xsrf = readCookie('XSRF-TOKEN');
  if (xsrf) headers['X-XSRF-TOKEN'] = decodeURIComponent(xsrf);
  const sid = sessionId();
  if (sid) headers['X-Session-Id'] = sid;
  let body = init.body;
  if (init.json !== undefined) { headers['Content-Type'] = 'application/json'; body = JSON.stringify(init.json); }
  return fetch(PUBLIC_API + path, { method: init.method ?? 'GET', headers, body, credentials: 'include', keepalive: init.keepalive });
}
