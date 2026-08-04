export interface AuthMeResponse {
  isAuthenticated: boolean;
  displayName: string | null;
}

const anonymous: AuthMeResponse = { isAuthenticated: false, displayName: null };

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
    displayName: payload.displayName ?? null
  };
}
