import { useEffect, useState } from 'react';
import { format } from 'date-fns';

import { fetchClientConfig } from './api/clientConfig';
import { fetchStatus, type StatusResponse } from './api/status';
import { initialiseTelemetry } from './telemetry';

type LoadState = 'loading' | 'ready' | 'error';

export function App() {
  const [state, setState] = useState<LoadState>('loading');
  const [status, setStatus] = useState<StatusResponse | undefined>();
  const [signInEnabled, setSignInEnabled] = useState(false);

  useEffect(() => {
    const controller = new AbortController();

    async function load() {
      try {
        const config = await fetchClientConfig(controller.signal);
        initialiseTelemetry(config.applicationInsightsConnectionString);
        setSignInEnabled(config.signInEnabled);
        setStatus(await fetchStatus(controller.signal));
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
          {/* IDENTITY STUB: replaced by the real sign-in experience in the identity slice. */}
          <h2>You are signed out</h2>
          <p>
            Sign-in is not available yet. This placeholder confirms the app shell, the versioned API
            and browser telemetry are wired up end to end.
          </p>
          <p className="meta">
            Environment <strong>{status?.environment}</strong> · today is{' '}
            {format(new Date(), 'd MMMM yyyy')}
          </p>
          <button type="button" disabled={!signInEnabled}>
            Sign in (coming soon)
          </button>
        </section>
      )}
    </main>
  );
}

export default App;
