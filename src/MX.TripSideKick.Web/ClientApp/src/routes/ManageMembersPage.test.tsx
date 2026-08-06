import { describe, expect, it } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';

import { server } from '../mocks/server';
import { renderWithProviders } from '../testUtils/renderWithProviders';
import { ManageMembersPage } from './ManageMembersPage';

const ownerMember = { id: 'm-owner', tripId: 'trip-1', subjectId: 'subject-me', role: 2, eTag: 'e-owner' };
const editorMember = { id: 'm-editor', tripId: 'trip-1', subjectId: 'subject-other', role: 1, eTag: 'e-editor' };

describe('ManageMembersPage', () => {
  it('shows a warning instead of the management UI for non-owners', async () => {
    server.use(
      http.get('/v1/trips/:tripId/members', () =>
        HttpResponse.json([{ ...editorMember, subjectId: 'subject-me', role: 1 }])
      ),
      http.get('/v1/trips/:tripId/invitations', () => HttpResponse.json([]))
    );

    renderWithProviders(<ManageMembersPage />, {
      initialEntries: ['/trips/trip-1/members'],
      routePath: '/trips/:tripId/members'
    });

    expect(await screen.findByTestId('not-owner-warning')).toBeInTheDocument();
    expect(screen.queryByTestId('invite-form')).not.toBeInTheDocument();
  });

  it('lets an owner send an invitation and see it appear pending with a stubbed acceptance link', async () => {
    server.use(
      http.get('/v1/trips/:tripId/members', () => HttpResponse.json([ownerMember])),
      http.get('/v1/trips/:tripId/invitations', () => HttpResponse.json([])),
      http.post('/v1/trips/:tripId/invitations', async ({ request }) => {
        const body = (await request.json()) as { invitedEmail: string; role: number };
        return HttpResponse.json(
          {
            id: 'invitation-1',
            tripId: 'trip-1',
            invitedEmail: body.invitedEmail,
            role: body.role,
            status: 'pending',
            acceptanceUrl: 'https://dev.tripsidekick.app/invitations/accept?token=stub-token'
          },
          { status: 201 }
        );
      })
    );

    renderWithProviders(<ManageMembersPage />, {
      initialEntries: ['/trips/trip-1/members'],
      routePath: '/trips/:tripId/members'
    });

    await screen.findByTestId('invite-form');

    await userEvent.type(screen.getByTestId('invite-email-input'), 'friend@example.com');
    await userEvent.click(screen.getByTestId('send-invite-button'));

    expect(await screen.findByText('friend@example.com')).toBeInTheDocument();
    expect(screen.getByTestId('invitation-acceptance-link')).toHaveAttribute(
      'href',
      'https://dev.tripsidekick.app/invitations/accept?token=stub-token'
    );
  });

  it('hides the remove button for the last remaining owner (client-side last-owner protection)', async () => {
    server.use(
      http.get('/v1/trips/:tripId/members', () => HttpResponse.json([ownerMember])),
      http.get('/v1/trips/:tripId/invitations', () => HttpResponse.json([]))
    );

    renderWithProviders(<ManageMembersPage />, {
      initialEntries: ['/trips/trip-1/members'],
      routePath: '/trips/:tripId/members'
    });

    await screen.findByText('You');
    expect(screen.queryByTestId(`remove-member-${ownerMember.id}`)).not.toBeInTheDocument();
  });

  it('shows an error when a role change is rejected by the API (e.g. demoting the last owner)', async () => {
    server.use(
      http.get('/v1/trips/:tripId/members', () => HttpResponse.json([ownerMember, editorMember])),
      http.get('/v1/trips/:tripId/invitations', () => HttpResponse.json([])),
      http.put('/v1/trips/:tripId/members/:membershipId/role', () =>
        HttpResponse.json({ title: 'Cannot demote the last owner.' }, { status: 409 })
      )
    );

    renderWithProviders(<ManageMembersPage />, {
      initialEntries: ['/trips/trip-1/members'],
      routePath: '/trips/:tripId/members'
    });

    const roleSelect = await screen.findByTestId(`role-select-${ownerMember.id}`);
    await userEvent.click(within(roleSelect).getByRole('combobox'));
    await userEvent.click(await screen.findByRole('option', { name: 'Editor' }));

    await waitFor(() => expect(screen.getByRole('alert')).toBeInTheDocument());
  });
});
