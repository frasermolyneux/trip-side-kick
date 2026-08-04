import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import { http, HttpResponse } from 'msw';

import { App } from './App';
import { server } from './mocks/server';

describe('App', () => {
  it('renders the signed-out shell once the API responds', async () => {
    render(<App />);

    expect(await screen.findByRole('heading', { name: /you are signed out/i })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /sign in/i })).toHaveAttribute('href', '/v1/auth/login');
  });

  it('shows the environment reported by the versioned API', async () => {
    render(<App />);

    expect(await screen.findByText(/Test/)).toBeInTheDocument();
  });

  it('renders the signed-in shell with the display name from /v1/auth/me, never a token', async () => {
    server.use(
      http.get('/v1/auth/me', () =>
        HttpResponse.json({ isAuthenticated: true, displayName: 'Ada Lovelace' })
      )
    );

    render(<App />);

    expect(await screen.findByRole('heading', { name: /welcome back, ada lovelace/i })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /sign out/i })).toHaveAttribute('href', '/v1/auth/logout');
  });
});
