import { describe, expect, it } from 'vitest';
import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';

import { server } from '../mocks/server';
import { renderWithProviders } from '../testUtils/renderWithProviders';
import { TripItineraryPage } from './TripItineraryPage';

const trip = {
  id: 'trip-1',
  name: 'Sicily 2026',
  destinations: ['Palermo'],
  reportingCurrencyCode: 'EUR',
  dates: { status: 'confirmed', startDate: '2026-05-01', endDate: '2026-05-08' },
  coverImageUrl: null,
  eTag: 'W/"trip-etag"'
};

const editorMember = { id: 'mem-me', tripId: 'trip-1', subjectId: 'subject-me', role: 1, eTag: 'e-me' };
const viewerMember = { id: 'mem-me-v', tripId: 'trip-1', subjectId: 'subject-me', role: 0, eTag: 'e-me' };
const alice = { id: 'trav-alice', tripId: 'trip-1', displayName: 'Alice', linkedMembershipId: 'mem-me', eTag: 'e-alice' };

const idea = {
  id: 'item-1',
  tripId: 'trip-1',
  title: 'Boat trip',
  notes: null,
  location: null,
  schedule: { status: 'unscheduled', date: null, startTime: null, endTime: null },
  applicableTravellerIds: [],
  eTag: 'W/"item-etag"'
};

const filter = {
  id: 'filter-1',
  tripId: 'trip-1',
  membershipId: 'mem-me',
  mode: 'everyone',
  selectedTravellerIds: [],
  eTag: 'W/"filter-etag"'
};

function baseHandlers(role: 'editor' | 'viewer' = 'editor', items = [idea], feedEntries: unknown[] = []) {
  return [
    http.get('/v1/trips/trip-1', () => HttpResponse.json(trip)),
    http.get('/v1/trips/:tripId/members', () =>
      HttpResponse.json([role === 'editor' ? editorMember : viewerMember])
    ),
    http.get('/v1/trips/:tripId/travellers', () => HttpResponse.json([alice])),
    http.get('/v1/trips/:tripId/itinerary/items', () => HttpResponse.json(items)),
    http.get('/v1/trips/:tripId/itinerary/activity-feed', () => HttpResponse.json(feedEntries)),
    http.get('/v1/trips/:tripId/itinerary/traveller-filter', () => HttpResponse.json(filter))
  ];
}

describe('TripItineraryPage', () => {
  it('renders ideas and hides mutation controls for viewers', async () => {
    server.use(...baseHandlers('viewer'));
    renderWithProviders(<TripItineraryPage />, {
      initialEntries: ['/trips/trip-1/itinerary'],
      routePath: '/trips/:tripId/itinerary'
    });

    expect(await screen.findByText('Boat trip')).toBeInTheDocument();
    expect(screen.queryByTestId('create-idea-form')).not.toBeInTheDocument();
    expect(screen.queryByTestId(`schedule-item-${idea.id}`)).not.toBeInTheDocument();
    expect(screen.queryByTestId('delete-item')).not.toBeInTheDocument();
  });

  it('lets an editor create an idea', async () => {
    let created = false;
    server.use(
      ...baseHandlers('editor'),
      http.post('/v1/trips/:tripId/itinerary/items', async ({ request }) => {
        const body = (await request.json()) as { title: string };
        created = true;
        return HttpResponse.json(
          { ...idea, id: 'item-new', title: body.title, eTag: 'W/"new"' },
          { status: 201 }
        );
      })
    );

    renderWithProviders(<TripItineraryPage />, {
      initialEntries: ['/trips/trip-1/itinerary'],
      routePath: '/trips/:tripId/itinerary'
    });

    await screen.findByTestId('create-idea-form');
    await userEvent.type(screen.getByTestId('idea-title-input'), 'Aperitivo');
    await userEvent.click(screen.getByTestId('create-idea-submit'));

    await waitFor(() => expect(created).toBe(true));
  });

  it('lets a viewer add a comment', async () => {
    let commented = false;
    server.use(
      ...baseHandlers('viewer'),
      http.get('/v1/trips/:tripId/itinerary/items/:itemId/comments', () => HttpResponse.json([])),
      http.post('/v1/trips/:tripId/itinerary/items/:itemId/comments', async () => {
        commented = true;
        return HttpResponse.json(
          {
            id: 'c-1',
            tripId: 'trip-1',
            itineraryItemId: 'item-1',
            authorDisplayName: 'Trip member',
            body: 'sounds good',
            createdAt: new Date().toISOString()
          },
          { status: 201 }
        );
      })
    );

    renderWithProviders(<TripItineraryPage />, {
      initialEntries: ['/trips/trip-1/itinerary'],
      routePath: '/trips/:tripId/itinerary'
    });

    await screen.findByText('Boat trip');
    await userEvent.click(screen.getByTestId('toggle-comments'));
    await userEvent.type(await screen.findByTestId('comment-input'), 'sounds good');
    await userEvent.click(screen.getByTestId('submit-comment'));

    await waitFor(() => expect(commented).toBe(true));
  });

  it('persists the traveller filter selection', async () => {
    let updateBody: { mode: string } | null = null;
    server.use(
      ...baseHandlers('editor'),
      http.put('/v1/trips/:tripId/itinerary/traveller-filter', async ({ request }) => {
        updateBody = (await request.json()) as { mode: string };
        return HttpResponse.json({ ...filter, mode: 'me', eTag: 'W/"filter-etag-2"' });
      })
    );

    renderWithProviders(<TripItineraryPage />, {
      initialEntries: ['/trips/trip-1/itinerary'],
      routePath: '/trips/:tripId/itinerary'
    });

    await screen.findByTestId('filter-me');
    await userEvent.click(screen.getByTestId('filter-me'));

    await waitFor(() => expect(updateBody?.mode).toBe('me'));
  });
});
