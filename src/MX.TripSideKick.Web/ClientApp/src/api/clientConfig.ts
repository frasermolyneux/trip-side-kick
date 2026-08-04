export interface ClientConfig {
  applicationInsightsConnectionString: string | null;
  signInEnabled: boolean;
  loginUrl: string;
  logoutUrl: string;
}

const defaultConfig: ClientConfig = {
  applicationInsightsConnectionString: null,
  signInEnabled: false,
  loginUrl: '/v1/auth/login',
  logoutUrl: '/v1/auth/logout'
};

/**
 * Fetches runtime configuration from the BFF. Configuration is served at runtime rather than
 * baked into the bundle so the same artefact can be promoted between environments.
 */
export async function fetchClientConfig(signal?: AbortSignal): Promise<ClientConfig> {
  const response = await fetch('/v1/client-config', {
    headers: { Accept: 'application/json' },
    credentials: 'same-origin',
    signal
  });

  if (!response.ok) {
    return defaultConfig;
  }

  const payload = (await response.json()) as Partial<ClientConfig>;

  return {
    applicationInsightsConnectionString: payload.applicationInsightsConnectionString ?? null,
    signInEnabled: payload.signInEnabled ?? false,
    loginUrl: payload.loginUrl ?? defaultConfig.loginUrl,
    logoutUrl: payload.logoutUrl ?? defaultConfig.logoutUrl
  };
}
