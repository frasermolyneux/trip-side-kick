import createClient from 'openapi-fetch';

import { fetchAntiforgeryToken, invalidateAntiforgeryToken } from './auth';
import type { paths } from './generated/schema';

/**
 * Typed fetch client for the `/v1` API, generated from the ASP.NET Core OpenAPI document (see
 * `npm run generate-api`). BFF/session-cookie model: no bearer token is ever attached here - the
 * browser only holds the `__Host-` session cookie, so every request must set
 * `credentials: 'same-origin'`.
 *
 * `openapi-fetch` builds requests via `new Request(url, ...)`, which (unlike the native
 * `fetch(url)`) requires an absolute URL - it does not resolve relative paths against
 * `window.location` itself. Using `window.location.origin` keeps every request same-origin (so
 * the session cookie is always sent) while working identically in the browser and under jsdom.
 *
 * `fetch` is passed as a thin wrapper (rather than relying on openapi-fetch's own
 * `globalThis.fetch` default) so it always resolves `globalThis.fetch` at call time - this module
 * is imported (and `createClient` invoked) once at startup, before MSW's `server.listen()` patches
 * `globalThis.fetch` in tests; capturing the reference eagerly would silently bypass every mock.
 */
export const apiClient = createClient<paths>({
  baseUrl: typeof window !== 'undefined' ? window.location.origin : '',
  credentials: 'same-origin',
  fetch: (...args: Parameters<typeof fetch>) => globalThis.fetch(...args)
});

/** HTTP methods that mutate state and are therefore guarded by `[ValidateAntiForgeryToken]`. */
const mutatingMethods = new Set(['POST', 'PUT', 'PATCH', 'DELETE']);

/**
 * Attaches the `X-CSRF-TOKEN` header to every mutating `/v1` request, mirroring the
 * `[ValidateAntiForgeryToken]` attribute on the corresponding controller actions (see
 * `AuthController` for the reference pattern). The token is fetched once from
 * `GET /v1/auth/antiforgery` and cached (see `fetchAntiforgeryToken`); a `400` here almost always
 * means the cached token expired alongside the antiforgery cookie (e.g. a long-lived tab), so the
 * cache is cleared to force a fresh token on the next attempt.
 */
apiClient.use({
  async onRequest({ request }) {
    if (mutatingMethods.has(request.method)) {
      const token = await fetchAntiforgeryToken();
      request.headers.set('X-CSRF-TOKEN', token);
    }

    return request;
  },
  onResponse({ response }) {
    if (response.status === 400) {
      invalidateAntiforgeryToken();
    }

    return response;
  }
});
