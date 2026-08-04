import { useEffect, useState } from 'react';
import { format } from 'date-fns';

import { fetchAuthMe, signOut, type AuthMeResponse } from './api/auth';
import { fetchClientConfig, type ClientConfig } from './api/clientConfig';
import { fetchStatus, type StatusResponse } from './api/status';
import { initialiseTelemetry } from './telemetry';

type LoadState = 'loading' | 'ready' | 'error';

const anonymousAuth: AuthMeResponse = { isAuthenticated: false, displayName: null };

export function App() {
  const [state, setState] = useState<LoadState>('loading');
  const [status, setStatus] = useState<StatusResponse | undefined>();
  const [config, setConfig] = useState<ClientConfig | undefined>();
  const [auth, setAuth] = useState<AuthMeResponse>(anonymousAuth);

  useEffect(() => {
    const controller = new AbortController();

    async function load() {
      try {
        const clientConfig = await fetchClientConfig(controller.signal);
        initialiseTelemetry(clientConfig.applicationInsightsConnectionString);
        setConfig(clientConfig);
        setStatus(await fetchStatus(controller.signal));
        // Never store the result anywhere but component state: no tokens are returned, only the
        // display name and a boolean - see docs/identity-and-access.md.
        setAuth(await fetchAuthMe(controller.signal));
        setState('ready');
      } catch {
        if (!controller.signal.aborted) {
          setState('error');
        }
      }
    }

    void load();

    return () => controller.abort();
  }, []);

  return (
    <main className="app-shell">
      <header>
        <h1>Trip Side Kick</h1>
        <p className="tagline">Your itinerary, sorted.</p>
      </header>

      {state === 'loading' && <p role="status">Loading…</p>}
      {state === 'error' && <p role="alert">We could not reach the service. Please try again.</p>}

      {state === 'ready' && (
        <section className="placeholder">
          {auth.isAuthenticated ? (
            <>
              <h2>Welcome back{auth.displayName ? `, ${auth.displayName}` : ''}</h2>
              <p>You are signed in. Trip planning lands in a later slice.</p>
              <button
                type="button"
                className="button"
                onClick={() => void signOut(config?.logoutUrl ?? '/v1/auth/logout')}
              >
                Sign out
              </button>
            </>
          ) : (
            <>
              <h2>You are signed out</h2>
              <p>Sign in with an email one-time passcode or a personal Microsoft account.</p>
              <a
                className="button"
                href={config?.loginUrl ?? '/v1/auth/login'}
                aria-disabled={!(config?.signInEnabled ?? false)}
                onClick={(event) => {
                  if (!(config?.signInEnabled ?? false)) {
                    event.preventDefault();
                  }
                }}
              >
                Sign in
              </a>
            </>
          )}
          <p className="meta">
            Environment <strong>{status?.environment}</strong> · today is{' '}
            {format(new Date(), 'd MMMM yyyy')}
          </p>
        </section>
      )}
    </main>
  );
}

export default App;
