import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { apiClient } from '../api/client';
import type { CreateTripRequest, TripResponse, UpdateTripRequest } from '../api/types';
import { unwrap } from '../api/unwrap';
import { queryKeys } from './queryKeys';

export function useTrips() {
  return useQuery({
    queryKey: queryKeys.trips,
    queryFn: async () => unwrap(await apiClient.GET('/v1/trips'))
  });
}

export function useTrip(tripId: string | undefined) {
  return useQuery({
    queryKey: queryKeys.trip(tripId ?? ''),
    queryFn: async () => unwrap(await apiClient.GET('/v1/trips/{tripId}', { params: { path: { tripId: tripId! } } })),
    enabled: Boolean(tripId)
  });
}

export function useCreateTrip() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (body: CreateTripRequest) => unwrap(await apiClient.POST('/v1/trips', { body })),
    onSuccess: (trip: TripResponse) => {
      queryClient.setQueryData(queryKeys.trip(trip.id), trip);
      void queryClient.invalidateQueries({ queryKey: queryKeys.trips });
    }
  });
}

export function useUpdateTrip(tripId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async ({ body, eTag }: { body: UpdateTripRequest; eTag: string }) =>
      unwrap(
        await apiClient.PUT('/v1/trips/{tripId}', {
          params: { path: { tripId } },
          headers: { 'If-Match': eTag },
          body
        })
      ),
    onSuccess: (trip: TripResponse) => {
      queryClient.setQueryData(queryKeys.trip(tripId), trip);
      void queryClient.invalidateQueries({ queryKey: queryKeys.trips });
    }
  });
}
