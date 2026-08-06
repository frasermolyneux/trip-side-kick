import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { apiClient } from '../api/client';
import type { CreateInvitationRequest, InvitationResponse } from '../api/types';
import { unwrap } from '../api/unwrap';
import { queryKeys } from './queryKeys';

export function useInvitations(tripId: string) {
  return useQuery({
    queryKey: queryKeys.invitations(tripId),
    queryFn: async () =>
      unwrap(await apiClient.GET('/v1/trips/{tripId}/invitations', { params: { path: { tripId } } })),
    enabled: Boolean(tripId)
  });
}

export function useCreateInvitation(tripId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (body: CreateInvitationRequest) =>
      unwrap(await apiClient.POST('/v1/trips/{tripId}/invitations', { params: { path: { tripId } }, body })),
    onSuccess: (invitation: InvitationResponse) => {
      queryClient.setQueryData(queryKeys.invitations(tripId), (previous: InvitationResponse[] | undefined) => [
        ...(previous ?? []),
        invitation
      ]);
    }
  });
}

export function useResendInvitation(tripId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (invitationId: string) =>
      unwrap(
        await apiClient.POST('/v1/trips/{tripId}/invitations/{invitationId}/resend', {
          params: { path: { tripId, invitationId } }
        })
      ),
    onSuccess: (invitation: InvitationResponse) => {
      queryClient.setQueryData(queryKeys.invitations(tripId), (previous: InvitationResponse[] | undefined) =>
        previous?.map((entry) => (entry.id === invitation.id ? invitation : entry))
      );
    }
  });
}

export function useRevokeInvitation(tripId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (invitationId: string) => {
      const result = await apiClient.POST('/v1/trips/{tripId}/invitations/{invitationId}/revoke', {
        params: { path: { tripId, invitationId } }
      });

      if (result.error) {
        throw new Error('Failed to revoke invitation.');
      }
    },
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.invitations(tripId) });
    }
  });
}

/** Not scoped to a trip - the acceptance token alone identifies the invitation. */
export function useAcceptInvitation() {
  return useMutation({
    mutationFn: async (acceptanceToken: string) =>
      unwrap(await apiClient.POST('/v1/invitations/accept', { body: { acceptanceToken } }))
  });
}
