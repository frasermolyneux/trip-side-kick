import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';

import { App } from './App';

describe('App', () => {
  it('renders the signed-out placeholder once the API responds', async () => {
    render(<App />);

    expect(await screen.findByRole('heading', { name: /you are signed out/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /sign in/i })).toBeDisabled();
  });

  it('shows the environment reported by the versioned API', async () => {
    render(<App />);

    expect(await screen.findByText(/Test/)).toBeInTheDocument();
  });
});
