import { useState } from 'react';
import { Link as RouterLink, useNavigate, useSearchParams } from 'react-router';
import { Alert, Box, Button, Typography } from '@mui/material';

import { useAuth } from '../auth/AuthContext';
import { useAcceptInvitation } from '../queries/invitations';

/**
 * Journey 2's accept-invitation flow. Reached via the acceptance link stubbed in
 * `InvitationResponse.acceptanceUrl` (real email delivery is not available yet - see
 * `IInvitationNotifier`). Only succeeds when the signed-in user's verified email matches the
 * invited email; a mismatch is refused server-side and surfaced here.
 */
export function AcceptInvitationPage() {
  const [searchParams] = useSearchParams();
  const token = searchParams.get('token');
  const { auth, config } = useAuth();
  const acceptInvitation = useAcceptInvitation();
  const navigate = useNavigate();
  const [error, setError] = useState<string | null>(null);

  if (!token) {
    return (
      <Alert severity="error" data-testid="invalid-invitation-link">
        This invitation link is missing its token.
      </Alert>
    );
  }

  if (!auth.isAuthenticated) {
    return (
      <Box data-testid="accept-invitation-signed-out">
        <Typography>Sign in to accept this invitation.</Typography>
        <Button
          variant="contained"
          href={`${config?.loginUrl ?? '/v1/auth/login'}?returnUrl=${encodeURIComponent(
            `/invitations/accept?token=${token}`
          )}`}
          data-testid="accept-invitation-sign-in"
        >
          Sign in
        </Button>
      </Box>
    );
  }

  async function handleAccept() {
    setError(null);
    try {
      const membership = await acceptInvitation.mutateAsync(token!);
      navigate(`/trips/${membership.tripId}`);
    } catch {
      setError(
        'This invitation could not be accepted. It may have already been used, revoked, or was sent to a different email address than the one you are signed in with.'
      );
    }
  }

  return (
    <Box data-testid="accept-invitation-page">
      <Typography variant="h4" component="h2" gutterBottom>
        Accept invitation
      </Typography>

      {error && (
        <Alert severity="error" data-testid="accept-invitation-error">
          {error}
        </Alert>
      )}

      <Button variant="contained" onClick={() => void handleAccept()} data-testid="accept-invitation-button">
        Accept
      </Button>
      <Button component={RouterLink} to="/trips" sx={{ ml: 2 }}>
        Cancel
      </Button>
    </Box>
  );
}

export default AcceptInvitationPage;
