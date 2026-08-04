export interface AuthMeResponse {
  isAuthenticated: boolean;
  displayName: string | null;
  subjectId: string | null;
}

const anonymous: AuthMeResponse = { isAuthenticated: false, displayName: null, subjectId: null };

/**
 * Reports the current sign-in state from the BFF session cookie. Never returns a token - the SPA
 * has no need for one, since the BFF terminates the OpenID Connect flow server-side.
 */
export async function fetchAuthMe(signal?: AbortSignal): Promise<AuthMeResponse> {
  const response = await fetch('/v1/auth/me', {
    headers: { Accept: 'application/json' },
    credentials: 'same-origin',
    signal
  });

  if (!response.ok) {
    return anonymous;
  }

  const payload = (await response.json()) as Partial<AuthMeResponse>;

  return {
    isAuthenticated: payload.isAuthenticated ?? false,
    displayName: payload.displayName ?? null,
    subjectId: payload.subjectId ?? null
  };
}

/**
 * Signs the user out. `/v1/auth/logout` is a POST guarded by an antiforgery token (not a plain
 * link) so a third-party page can't force a sign-out via a cross-site GET navigation. Submits a
 * real top-level form POST (rather than `fetch`) so the browser follows the resulting redirect
 * chain - including the cross-origin hop to the identity provider's end-session endpoint - exactly
 * as it would for any normal navigation; a `fetch`-based POST would hit CORS once the response
 * redirects off-origin.
 */
export async function signOut(logoutUrl: string): Promise<void> {
  const tokenResponse = await fetch('/v1/auth/antiforgery', {
    headers: { Accept: 'application/json' },
    credentials: 'same-origin'
  });

  if (!tokenResponse.ok) {
    throw new Error('Failed to acquire an antiforgery token for sign-out.');
  }

  const { token } = (await tokenResponse.json()) as { token: string };

  const form = document.createElement('form');
  form.method = 'POST';
  form.action = logoutUrl;
  form.style.display = 'none';

  const tokenField = document.createElement('input');
  tokenField.type = 'hidden';
  tokenField.name = '__RequestVerificationToken';
  tokenField.value = token;
  form.appendChild(tokenField);

  document.body.appendChild(form);
  form.submit();
}
