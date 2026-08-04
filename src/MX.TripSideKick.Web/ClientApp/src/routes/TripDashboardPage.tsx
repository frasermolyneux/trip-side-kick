import { useState } from 'react';
import { Link as RouterLink, useParams } from 'react-router-dom';
import {
  Alert,
  Box,
  Button,
  Chip,
  CircularProgress,
  List,
  ListItem,
  ListItemText,
  Stack,
  TextField,
  Typography
} from '@mui/material';

import { useAuth } from '../auth/AuthContext';
import { membershipRoleLabels, MembershipRole } from '../api/roles';
import { useLeaveTrip, useMembers } from '../queries/members';
import { useLinkSelfAsTraveller, useTravellers, useUnlinkSelfAsTraveller } from '../queries/travellers';
import { useTrip, useUpdateTrip } from '../queries/trips';

/** Journey 1/2 dashboard: setup completeness plus the member/traveller roster for a single trip. */
export function TripDashboardPage() {
  const { tripId = '' } = useParams();
  const { auth } = useAuth();
  const { data: trip, isLoading: tripLoading, isError: tripError } = useTrip(tripId);
  const { data: members } = useMembers(tripId);
  const { data: travellers } = useTravellers(tripId);
  const leaveTrip = useLeaveTrip(tripId);
  const linkSelf = useLinkSelfAsTraveller(tripId);
  const unlinkSelf = useUnlinkSelfAsTraveller(tripId);
  const updateTrip = useUpdateTrip(tripId);
  const [actionError, setActionError] = useState<string | null>(null);
  const [editingName, setEditingName] = useState(false);
  const [nameDraft, setNameDraft] = useState('');

  const myMembership = members?.find((member) => member.subjectId === auth.subjectId);
  const isOwner = myMembership?.role === MembershipRole.Owner;
  // Editors manage trip content (Journey 2); Viewers are read-only even when signed in.
  const canEditContent = myMembership?.role === MembershipRole.Owner || myMembership?.role === MembershipRole.Editor;
  const myTraveller = travellers?.find((traveller) => traveller.linkedMembershipId === myMembership?.id);

  if (tripLoading) {
    return <CircularProgress aria-label="Loading trip" />;
  }

  if (tripError || !trip) {
    return <Typography role="alert">We could not load this trip.</Typography>;
  }

  const setupItems = [
    { label: 'Name', complete: trip.name.trim().length > 0 },
    { label: 'Destinations', complete: trip.destinations.length > 0 },
    { label: 'Reporting currency', complete: Boolean(trip.reportingCurrencyCode) },
    { label: 'Dates', complete: trip.dates.status === 'confirmed' },
    { label: 'Cover image', complete: Boolean(trip.coverImageUrl) }
  ];

  async function handleLeave() {
    setActionError(null);
    try {
      await leaveTrip.mutateAsync();
    } catch {
      setActionError('The last owner of a trip cannot leave. Promote another member to Owner first.');
    }
  }

  function startEditingName() {
    setNameDraft(trip!.name);
    setEditingName(true);
  }

  async function handleSaveName() {
    setActionError(null);
    try {
      await updateTrip.mutateAsync({
        eTag: trip!.eTag,
        body: {
          name: nameDraft,
          destinations: trip!.destinations,
          reportingCurrencyCode: trip!.reportingCurrencyCode,
          dates: trip!.dates,
          coverImageUrl: trip!.coverImageUrl
        }
      });
      setEditingName(false);
    } catch {
      setActionError('Could not save the trip name. Viewers cannot edit trip content, and a concurrent edit may have changed it.');
    }
  }

  return (
    <Box data-testid="trip-dashboard-page">
      {editingName ? (
        <Stack direction="row" spacing={1} sx={{ alignItems: 'center', mb: 2 }}>
          <TextField
            size="small"
            label="Trip name"
            value={nameDraft}
            onChange={(event) => setNameDraft(event.target.value)}
            slotProps={{ htmlInput: { 'data-testid': 'trip-name-input' } }}
          />
          <Button onClick={() => void handleSaveName()} variant="contained" data-testid="save-trip-name">
            Save
          </Button>
          <Button onClick={() => setEditingName(false)}>Cancel</Button>
        </Stack>
      ) : (
        <Stack direction="row" spacing={1} sx={{ alignItems: 'center', mb: 2 }}>
          <Typography variant="h4" component="h2" data-testid="trip-name">
            {trip.name}
          </Typography>
          {canEditContent && (
            <Button size="small" onClick={startEditingName} data-testid="edit-trip-name">
              Edit
            </Button>
          )}
        </Stack>
      )}

      {trip.dates.status !== 'confirmed' && (
        <Alert severity="info" sx={{ mb: 2 }} data-testid="dates-not-confirmed-banner">
          Dates are {trip.dates.status}. Day-by-day scheduling unlocks once dates are confirmed.
        </Alert>
      )}

      <Typography variant="h6" component="h3">
        Setup completeness
      </Typography>
      <Stack direction="row" spacing={1} sx={{ mb: 2 }} data-testid="setup-completeness">
        {setupItems.map((item) => (
          <Chip
            key={item.label}
            label={item.label}
            color={item.complete ? 'success' : 'default'}
            variant={item.complete ? 'filled' : 'outlined'}
          />
        ))}
      </Stack>

      <Typography variant="h6" component="h3">
        Members
      </Typography>
      {actionError && <Alert severity="error">{actionError}</Alert>}
      <List data-testid="members-list">
        {members?.map((member) => (
          <ListItem key={member.id}>
            <ListItemText
              primary={member.subjectId === auth.subjectId ? 'You' : member.subjectId}
              secondary={membershipRoleLabels[member.role as 0 | 1 | 2]}
            />
          </ListItem>
        ))}
      </List>

      <Stack direction="row" spacing={2} sx={{ mt: 2 }}>
        {isOwner && (
          <Button component={RouterLink} to={`/trips/${tripId}/members`} variant="outlined" data-testid="manage-members-link">
            Manage members &amp; invitations
          </Button>
        )}
        {myTraveller ? (
          <Button onClick={() => void unlinkSelf.mutateAsync()} data-testid="remove-self-as-traveller">
            Remove myself as a traveller
          </Button>
        ) : (
          <Button onClick={() => void linkSelf.mutateAsync(null)} data-testid="add-self-as-traveller">
            Add myself as a traveller
          </Button>
        )}
        <Button color="error" onClick={() => void handleLeave()} data-testid="leave-trip-button">
          Leave trip
        </Button>
      </Stack>
    </Box>
  );
}

export default TripDashboardPage;
