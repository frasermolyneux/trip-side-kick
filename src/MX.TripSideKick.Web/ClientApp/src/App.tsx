import { useEffect, useState, type ReactNode } from 'react';
import { Navigate, Route, Routes, Link as RouterLink } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { AppBar, Box, Container, CssBaseline, Toolbar, Typography } from '@mui/material';
import { format } from 'date-fns';

import { fetchAuthMe, signOut, type AuthMeResponse } from './api/auth';
import { fetchClientConfig, type ClientConfig } from './api/clientConfig';
import { fetchStatus, type StatusResponse } from './api/status';
import { AuthContext } from './auth/AuthContext';
import { AcceptInvitationPage } from './routes/AcceptInvitationPage';
import { CreateTripPage } from './routes/CreateTripPage';
import { ManageMembersPage } from './routes/ManageMembersPage';
import { TripDashboardPage } from './routes/TripDashboardPage';
import { TripsListPage } from './routes/TripsListPage';
import { initialiseTelemetry } from './telemetry';

type LoadState = 'loading' | 'ready' | 'error';

const anonymousAuth: AuthMeResponse = { isAuthenticated: false, displayName: null, subjectId: null };

const queryClient = new QueryClient();

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
    <QueryClientProvider client={queryClient}>
      <AuthContext.Provider value={{ auth, config }}>
        <CssBaseline />
        {state === 'loading' && (
          <main className="app-shell">
            <p role="status">Loading…</p>
          </main>
        )}
        {state === 'error' && (
          <main className="app-shell">
            <p role="alert">We could not reach the service. Please try again.</p>
          </main>
        )}
        {state === 'ready' && (
          <Routes>
            <Route
              path="/"
              element={
                <main className="app-shell">
                  <header>
                    <h1>Trip Side Kick</h1>
                    <p className="tagline">Your itinerary, sorted.</p>
                  </header>
                  <section className="placeholder">
                    {auth.isAuthenticated ? (
                      <>
                        <h2>Welcome back{auth.displayName ? `, ${auth.displayName}` : ''}</h2>
                        <p>
                          You are signed in. <RouterLink to="/trips">Go to your trips</RouterLink>.
                        </p>
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
                </main>
              }
            />
            <Route path="/trips" element={<RequireAuth config={config} auth={auth}><AppShell><TripsListPage /></AppShell></RequireAuth>} />
            <Route path="/trips/new" element={<RequireAuth config={config} auth={auth}><AppShell><CreateTripPage /></AppShell></RequireAuth>} />
            <Route path="/trips/:tripId" element={<RequireAuth config={config} auth={auth}><AppShell><TripDashboardPage /></AppShell></RequireAuth>} />
            <Route
              path="/trips/:tripId/members"
              element={<RequireAuth config={config} auth={auth}><AppShell><ManageMembersPage /></AppShell></RequireAuth>}
            />
            <Route path="/invitations/accept" element={<AppShell><AcceptInvitationPage /></AppShell>} />
            <Route path="*" element={<Navigate to="/" replace />} />
          </Routes>
        )}
      </AuthContext.Provider>
    </QueryClientProvider>
  );
}

/**
 * The app routes (everything except `/` and `/invitations/accept`) require a signed-in session -
 * an anonymous visitor is redirected to sign in rather than allowed to render a page that would
 * just fail every API call with 401 (see docs/identity-and-access.md).
 */
function RequireAuth({
  auth,
  config,
  children
}: {
  auth: AuthMeResponse;
  config: ClientConfig | undefined;
  children: ReactNode;
}) {
  if (!auth.isAuthenticated) {
    window.location.href = config?.loginUrl ?? '/v1/auth/login';
    return (
      <main className="app-shell" data-testid="redirecting-to-sign-in">
        <p role="status">Redirecting to sign in…</p>
      </main>
    );
  }

  return <>{children}</>;
}

function AppShell({ children }: { children: ReactNode }) {
  return (
    <Box>
      <AppBar position="static">
        <Toolbar>
          <Typography variant="h6" component={RouterLink} to="/trips" sx={{ flexGrow: 1, color: 'inherit', textDecoration: 'none' }}>
            Trip Side Kick
          </Typography>
        </Toolbar>
      </AppBar>
      <Container sx={{ py: 3 }}>{children}</Container>
    </Box>
  );
}

export default App;
