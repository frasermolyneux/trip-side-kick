import type { ReactElement } from 'react';
import { render } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

import { AuthContext, type AuthContextValue } from '../auth/AuthContext';

const defaultAuth: AuthContextValue = {
  auth: { isAuthenticated: true, displayName: 'Test User', subjectId: 'subject-me' },
  config: {
    applicationInsightsConnectionString: null,
    signInEnabled: true,
    loginUrl: '/v1/auth/login',
    logoutUrl: '/v1/auth/logout'
  }
};

/**
 * Renders a route component wrapped with the same providers `App.tsx` supplies at runtime
 * (query client, router, auth context) so page components under test see realistic context.
 * Pass `routePath` (e.g. `/trips/:tripId`) when the component reads `useParams`.
 */
export function renderWithProviders(
  ui: ReactElement,
  options: {
    auth?: Partial<AuthContextValue['auth']>;
    initialEntries?: string[];
    routePath?: string;
  } = {}
) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const authValue: AuthContextValue = {
    ...defaultAuth,
    auth: { ...defaultAuth.auth, ...options.auth }
  };

  const content = options.routePath ? (
    <Routes>
      <Route path={options.routePath} element={ui} />
    </Routes>
  ) : (
    ui
  );

  return render(
    <QueryClientProvider client={queryClient}>
      <AuthContext.Provider value={authValue}>
        <MemoryRouter initialEntries={options.initialEntries ?? ['/']}>{content}</MemoryRouter>
      </AuthContext.Provider>
    </QueryClientProvider>
  );
}

