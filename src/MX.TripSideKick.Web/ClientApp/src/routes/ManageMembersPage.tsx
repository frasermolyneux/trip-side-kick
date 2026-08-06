import { useState } from 'react';
import { useParams } from 'react-router';
import { zodResolver } from '@hookform/resolvers/zod';
import { Controller, useForm } from 'react-hook-form';
import { z } from 'zod';
import {
  Alert,
  Box,
  Button,
  Chip,
  IconButton,
  List,
  ListItem,
  ListItemText,
  MenuItem,
  Stack,
  TextField,
  Typography
} from '@mui/material';

import { useAuth } from '../auth/AuthContext';
import { MembershipRole, membershipRoleLabels, TravellerLinkKind } from '../api/roles';
import { useChangeRole, useMembers, useRemoveMember } from '../queries/members';
import { useCreateInvitation, useInvitations, useResendInvitation, useRevokeInvitation } from '../queries/invitations';

const inviteSchema = z.object({
  invitedEmail: z.string().trim().email('Enter a valid email address.'),
  role: z.union([z.literal(MembershipRole.Editor), z.literal(MembershipRole.Viewer)])
});

type InviteFormValues = z.infer<typeof inviteSchema>;

/** Journey 2: Owner-only membership + invitation management for a single trip. */
export function ManageMembersPage() {
  const { tripId = '' } = useParams();
  const { auth } = useAuth();
  const { data: members } = useMembers(tripId);
  const { data: invitations } = useInvitations(tripId);
  const changeRole = useChangeRole(tripId);
  const removeMember = useRemoveMember(tripId);
  const createInvitation = useCreateInvitation(tripId);
  const resendInvitation = useResendInvitation(tripId);
  const revokeInvitation = useRevokeInvitation(tripId);
  const [error, setError] = useState<string | null>(null);

  const myMembership = members?.find((member) => member.subjectId === auth.subjectId);
  const isOwner = myMembership?.role === MembershipRole.Owner;

  const {
    control,
    handleSubmit,
    reset,
    formState: { errors, isSubmitting }
  } = useForm<InviteFormValues>({
    resolver: zodResolver(inviteSchema),
    defaultValues: { invitedEmail: '', role: MembershipRole.Editor }
  });

  const onInvite = handleSubmit(async (values) => {
    setError(null);
    try {
      await createInvitation.mutateAsync({
        invitedEmail: values.invitedEmail,
        role: values.role,
        linkKind: TravellerLinkKind.NonTravellingPlanner,
        existingTravellerId: null,
        newTravellerDisplayName: null
      });
      reset();
    } catch {
      setError('Could not send the invitation. Check the email address and try again.');
    }
  });

  async function handleChangeRole(membershipId: string, role: number, eTag: string) {
    setError(null);
    try {
      await changeRole.mutateAsync({ membershipId, role, eTag });
    } catch {
      setError('Could not change that member\u2019s role. The last Owner cannot be demoted.');
    }
  }

  async function handleRemove(membershipId: string) {
    setError(null);
    try {
      await removeMember.mutateAsync(membershipId);
    } catch {
      setError('Could not remove that member. The last Owner cannot be removed.');
    }
  }

  if (members && !isOwner) {
    return (
      <Alert severity="warning" data-testid="not-owner-warning">
        Only Owners can manage members and invitations.
      </Alert>
    );
  }

  return (
    <Box data-testid="manage-members-page">
      <Typography variant="h4" component="h2" gutterBottom>
        Members &amp; invitations
      </Typography>

      {error && <Alert severity="error">{error}</Alert>}

      <Typography variant="h6" component="h3">
        Members
      </Typography>
      <List data-testid="members-list">
        {members?.map((member) => (
          <ListItem
            key={member.id}
            secondaryAction={
              member.role !== MembershipRole.Owner || members.filter((m) => m.role === MembershipRole.Owner).length > 1 ? (
                <IconButton
                  aria-label={`Remove ${member.subjectId}`}
                  onClick={() => void handleRemove(member.id)}
                  data-testid={`remove-member-${member.id}`}
                >
                  &times;
                </IconButton>
              ) : undefined
            }
          >
            <ListItemText primary={member.subjectId === auth.subjectId ? 'You' : member.subjectId} />
            <TextField
              select
              size="small"
              value={member.role}
              onChange={(event) => void handleChangeRole(member.id, Number(event.target.value), member.eTag)}
              slotProps={{ select: { 'data-testid': `role-select-${member.id}` } as never }}
            >
              {Object.entries(membershipRoleLabels).map(([value, label]) => (
                <MenuItem key={value} value={value}>
                  {label}
                </MenuItem>
              ))}
            </TextField>
          </ListItem>
        ))}
      </List>

      <Typography variant="h6" component="h3" sx={{ mt: 3 }}>
        Invite someone
      </Typography>
      <Box component="form" onSubmit={onInvite} data-testid="invite-form">
        <Stack direction="row" spacing={2} sx={{ alignItems: 'flex-start', maxWidth: 560 }}>
          <Controller
            name="invitedEmail"
            control={control}
            render={({ field }) => (
              <TextField
                {...field}
                label="Email"
                error={Boolean(errors.invitedEmail)}
                helperText={errors.invitedEmail?.message}
                slotProps={{ htmlInput: { 'data-testid': 'invite-email-input' } }}
              />
            )}
          />
          <Controller
            name="role"
            control={control}
            render={({ field }) => (
              <TextField {...field} select label="Role" slotProps={{ select: { 'data-testid': 'invite-role-select' } as never }}>
                <MenuItem value={MembershipRole.Editor}>Editor</MenuItem>
                <MenuItem value={MembershipRole.Viewer}>Viewer</MenuItem>
              </TextField>
            )}
          />
          <Button type="submit" variant="contained" disabled={isSubmitting} data-testid="send-invite-button">
            Invite
          </Button>
        </Stack>
      </Box>

      <Typography variant="h6" component="h3" sx={{ mt: 3 }}>
        Invitations
      </Typography>
      <List data-testid="invitations-list">
        {invitations?.map((invitation) => (
          <ListItem key={invitation.id} data-testid={`invitation-${invitation.id}`}>
            <ListItemText
              primary={invitation.invitedEmail}
              slotProps={{ secondary: { component: 'div' } }}
              secondary={
                <>
                  <Chip size="small" label={invitation.status} sx={{ mr: 1 }} data-testid="invitation-status" />
                  {membershipRoleLabels[invitation.role as 0 | 1 | 2]}
                  {invitation.status === 'pending' && (
                    <>
                      {' \u2014 '}
                      <a
                        href={invitation.acceptanceUrl}
                        data-testid="invitation-acceptance-link"
                        onClick={(event) => event.preventDefault()}
                      >
                        acceptance link
                      </a>
                    </>
                  )}
                </>
              }
            />
            {invitation.status === 'pending' && (
              <Stack direction="row" spacing={1}>
                <Button
                  size="small"
                  onClick={() => void resendInvitation.mutateAsync(invitation.id)}
                  data-testid={`resend-invitation-${invitation.id}`}
                >
                  Resend
                </Button>
                <Button
                  size="small"
                  color="error"
                  onClick={() => void revokeInvitation.mutateAsync(invitation.id)}
                  data-testid={`revoke-invitation-${invitation.id}`}
                >
                  Revoke
                </Button>
              </Stack>
            )}
          </ListItem>
        ))}
      </List>
    </Box>
  );
}

export default ManageMembersPage;
