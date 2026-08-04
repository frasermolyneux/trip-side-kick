import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { apiClient } from '../api/client';
import type { MembershipResponse } from '../api/types';
import { unwrap } from '../api/unwrap';
import { queryKeys } from './queryKeys';

export function useMembers(tripId: string) {
  return useQuery({
    queryKey: queryKeys.members(tripId),
    queryFn: async () => unwrap(await apiClient.GET('/v1/trips/{tripId}/members', { params: { path: { tripId } } })),
    enabled: Boolean(tripId)
  });
}

export function useChangeRole(tripId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async ({ membershipId, role, eTag }: { membershipId: string; role: number; eTag: string }) =>
      unwrap(
        await apiClient.PUT('/v1/trips/{tripId}/members/{membershipId}/role', {
          params: { path: { tripId, membershipId } },
          headers: { 'If-Match': eTag },
          body: { role }
        })
      ),
    onSuccess: (membership: MembershipResponse) => {
      queryClient.setQueryData(queryKeys.members(tripId), (previous: MembershipResponse[] | undefined) =>
        previous?.map((entry) => (entry.id === membership.id ? membership : entry))
      );
    }
  });
}

export function useRemoveMember(tripId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (membershipId: string) => {
      const result = await apiClient.DELETE('/v1/trips/{tripId}/members/{membershipId}', {
        params: { path: { tripId, membershipId } }
      });

      if (result.error) {
        throw new Error('Failed to remove member.');
      }
    },
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.members(tripId) });
    }
  });
}

export function useLeaveTrip(tripId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async () => {
      const result = await apiClient.POST('/v1/trips/{tripId}/members/leave', { params: { path: { tripId } } });

      if (result.error) {
        throw new Error('Failed to leave trip.');
      }
    },
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.trips });
    }
  });
}
