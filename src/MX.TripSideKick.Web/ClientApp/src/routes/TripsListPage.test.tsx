import { describe, expect, it } from 'vitest';
import { screen, waitFor } from '@testing-library/react';
import { http, HttpResponse } from 'msw';

import { server } from '../mocks/server';
import { renderWithProviders } from '../testUtils/renderWithProviders';
import { TripsListPage } from './TripsListPage';

describe('TripsListPage', () => {
  it('shows an empty state when the signed-in user has no trips', async () => {
    server.use(http.get('/v1/trips', () => HttpResponse.json([])));

    renderWithProviders(<TripsListPage />);

    expect(await screen.findByTestId('no-trips-message')).toBeInTheDocument();
    expect(screen.getByTestId('create-trip-link')).toHaveAttribute('href', '/trips/new');
  });

  it('lists trips returned by the API', async () => {
    server.use(
      http.get('/v1/trips', () =>
        HttpResponse.json([
          { id: 'trip-1', name: 'Iceland Ring Road', destinations: ['Reykjavik', 'Vik'] },
          { id: 'trip-2', name: 'Weekend in Rome', destinations: [] }
        ])
      )
    );

    renderWithProviders(<TripsListPage />);

    expect(await screen.findAllByTestId('trip-list-item')).toHaveLength(2);
    expect(screen.getByText('Iceland Ring Road')).toBeInTheDocument();
    expect(screen.getByText('Weekend in Rome')).toBeInTheDocument();
  });

  it('shows an error state when the trips request fails', async () => {
    server.use(http.get('/v1/trips', () => HttpResponse.json({ title: 'Boom' }, { status: 500 })));

    renderWithProviders(<TripsListPage />);

    await waitFor(() => expect(screen.getByRole('alert')).toBeInTheDocument());
  });
});
