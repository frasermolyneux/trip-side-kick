import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { apiClient } from '../api/client';
import { unwrap } from '../api/unwrap';
import { queryKeys } from './queryKeys';

export function useTravellers(tripId: string) {
  return useQuery({
    queryKey: queryKeys.travellers(tripId),
    queryFn: async () =>
      unwrap(await apiClient.GET('/v1/trips/{tripId}/travellers', { params: { path: { tripId } } })),
    enabled: Boolean(tripId)
  });
}

export function useLinkSelfAsTraveller(tripId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (displayName: string | null) =>
      unwrap(
        await apiClient.POST('/v1/trips/{tripId}/travellers/self', {
          params: { path: { tripId } },
          body: { displayName }
        })
      ),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.travellers(tripId) });
    }
  });
}

export function useUnlinkSelfAsTraveller(tripId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async () => {
      const result = await apiClient.DELETE('/v1/trips/{tripId}/travellers/self', { params: { path: { tripId } } });

      if (result.error) {
        throw new Error('Failed to unlink self as traveller.');
      }
    },
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.travellers(tripId) });
    }
  });
}
