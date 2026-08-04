import { describe, expect, it } from 'vitest';
import { screen, waitFor } from '@testing-library/react';
import { http, HttpResponse } from 'msw';

import { server } from '../mocks/server';
import { renderWithProviders } from '../testUtils/renderWithProviders';
import { TripDashboardPage } from './TripDashboardPage';

const trip = {
  id: 'trip-1',
  name: 'Iceland Ring Road',
  destinations: ['Reykjavik'],
  reportingCurrencyCode: null,
  dates: { status: 'undecided', startDate: null, endDate: null },
  coverImageUrl: null
};

function ownerMember(subjectId: string) {
  return { id: 'membership-owner', tripId: 'trip-1', subjectId, role: 2, eTag: 'etag-owner' };
}

describe('TripDashboardPage', () => {
  it('shows setup completeness and the members list', async () => {
    server.use(
      http.get('/v1/trips/:tripId', () => HttpResponse.json(trip)),
      http.get('/v1/trips/:tripId/members', () => HttpResponse.json([ownerMember('subject-me')])),
      http.get('/v1/trips/:tripId/travellers', () => HttpResponse.json([]))
    );

    renderWithProviders(<TripDashboardPage />, {
      initialEntries: ['/trips/trip-1'],
      routePath: '/trips/:tripId'
    });

    expect(await screen.findByTestId('trip-name')).toHaveTextContent('Iceland Ring Road');
    expect(screen.getByTestId('dates-not-confirmed-banner')).toBeInTheDocument();
    expect(await screen.findByText('You')).toBeInTheDocument();
  });

  it('shows the manage-members link only for owners', async () => {
    server.use(
      http.get('/v1/trips/:tripId', () => HttpResponse.json(trip)),
      http.get('/v1/trips/:tripId/members', () =>
        HttpResponse.json([{ id: 'm1', tripId: 'trip-1', subjectId: 'someone-else', role: 1, eTag: 'e1' }])
      ),
      http.get('/v1/trips/:tripId/travellers', () => HttpResponse.json([]))
    );

    renderWithProviders(<TripDashboardPage />, {
      initialEntries: ['/trips/trip-1'],
      routePath: '/trips/:tripId'
    });

    await screen.findByTestId('trip-name');
    expect(screen.queryByTestId('manage-members-link')).not.toBeInTheDocument();
  });

  it('shows an error state when the trip cannot be loaded', async () => {
    server.use(http.get('/v1/trips/:tripId', () => HttpResponse.json({ title: 'Not Found' }, { status: 404 })));

    renderWithProviders(<TripDashboardPage />, {
      initialEntries: ['/trips/trip-1'],
      routePath: '/trips/:tripId'
    });

    await waitFor(() => expect(screen.getByRole('alert')).toBeInTheDocument());
  });
});
