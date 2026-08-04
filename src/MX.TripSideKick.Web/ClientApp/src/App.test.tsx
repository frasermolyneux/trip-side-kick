import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { http, HttpResponse } from 'msw';

import { App } from './App';
import { server } from './mocks/server';

function renderApp() {
  return render(
    <MemoryRouter initialEntries={['/']}>
      <App />
    </MemoryRouter>
  );
}

describe('App', () => {
  it('renders the signed-out shell once the API responds', async () => {
    renderApp();

    expect(await screen.findByRole('heading', { name: /you are signed out/i })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /sign in/i })).toHaveAttribute('href', '/v1/auth/login');
  });

  it('shows the environment reported by the versioned API', async () => {
    renderApp();

    expect(await screen.findByText(/Test/)).toBeInTheDocument();
  });

  it('renders the signed-in shell with the display name from /v1/auth/me, never a token', async () => {
    server.use(
      http.get('/v1/auth/me', () =>
        HttpResponse.json({ isAuthenticated: true, displayName: 'Ada Lovelace', subjectId: 'subject-1' })
      )
    );

    renderApp();

    expect(await screen.findByRole('heading', { name: /welcome back, ada lovelace/i })).toBeInTheDocument();
    // Sign-out is a POST (guarded by an antiforgery token) rather than a plain link, so a
    // third-party page can't force a sign-out via a cross-site GET navigation.
    expect(screen.getByRole('button', { name: /sign out/i })).toBeInTheDocument();
  });

  it('disables navigation on the sign-in link when the client config reports sign-in is not enabled', async () => {
    server.use(
      http.get('/v1/client-config', () =>
        HttpResponse.json({
          applicationInsightsConnectionString: null,
          signInEnabled: false,
          loginUrl: '/v1/auth/login',
          logoutUrl: '/v1/auth/logout'
        })
      )
    );

    renderApp();

    const signIn = await screen.findByRole('link', { name: /sign in/i });
    expect(signIn).toHaveAttribute('aria-disabled', 'true');

    const clickEvent = new MouseEvent('click', { bubbles: true, cancelable: true });
    signIn.dispatchEvent(clickEvent);
    expect(clickEvent.defaultPrevented).toBe(true);
  });
});
