import { describe, expect, it } from 'vitest';
import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';

import { server } from '../mocks/server';
import { renderWithProviders } from '../testUtils/renderWithProviders';
import { AcceptInvitationPage } from './AcceptInvitationPage';

describe('AcceptInvitationPage', () => {
  it('shows a sign-in prompt (preserving the return URL) when the visitor is anonymous', () => {
    renderWithProviders(<AcceptInvitationPage />, {
      initialEntries: ['/invitations/accept?token=abc123'],
      auth: { isAuthenticated: false, subjectId: null, displayName: null }
    });

    const signIn = screen.getByTestId('accept-invitation-sign-in');
    expect(signIn).toHaveAttribute(
      'href',
      expect.stringContaining(encodeURIComponent('/invitations/accept?token=abc123'))
    );
  });

  it('shows an error when the link is missing its token', () => {
    renderWithProviders(<AcceptInvitationPage />, { initialEntries: ['/invitations/accept'] });

    expect(screen.getByTestId('invalid-invitation-link')).toBeInTheDocument();
  });

  it('accepts the invitation and lets the caller navigate to the trip', async () => {
    server.use(
      http.post('/v1/invitations/accept', () =>
        HttpResponse.json({ id: 'membership-1', tripId: 'trip-1', subjectId: 'subject-me', role: 1, eTag: 'e1' })
      )
    );

    renderWithProviders(<AcceptInvitationPage />, { initialEntries: ['/invitations/accept?token=abc123'] });

    await userEvent.click(screen.getByTestId('accept-invitation-button'));

    await waitFor(() => expect(screen.queryByTestId('accept-invitation-error')).not.toBeInTheDocument());
  });

  it('shows an error when the signed-in identity does not match the invited email', async () => {
    server.use(
      http.post('/v1/invitations/accept', () =>
        HttpResponse.json({ title: 'The invitation email does not match the signed-in account.' }, { status: 409 })
      )
    );

    renderWithProviders(<AcceptInvitationPage />, { initialEntries: ['/invitations/accept?token=abc123'] });

    await userEvent.click(screen.getByTestId('accept-invitation-button'));

    expect(await screen.findByTestId('accept-invitation-error')).toHaveTextContent(/different email address/i);
  });
});
