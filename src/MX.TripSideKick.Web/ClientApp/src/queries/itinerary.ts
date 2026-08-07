import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';

import { apiClient } from '../api/client';
import type {
  AddCommentRequest,
  CreateItineraryItemRequest,
  ItineraryItemResponse,
  ScheduleItineraryItemRequest,
  SetApplicabilityRequest,
  SetTravellerFilterRequest,
  UpdateItineraryItemContentRequest
} from '../api/types';
import { unwrap } from '../api/unwrap';
import { queryKeys } from './queryKeys';

// ---------- Items ----------

export function useItineraryItems(tripId: string) {
  return useQuery({
    queryKey: queryKeys.itineraryItems(tripId),
    queryFn: async () =>
      unwrap(await apiClient.GET('/v1/trips/{tripId}/itinerary/items', { params: { path: { tripId } } })),
    enabled: Boolean(tripId)
  });
}

export function useCreateItineraryItem(tripId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (body: CreateItineraryItemRequest) =>
      unwrap(
        await apiClient.POST('/v1/trips/{tripId}/itinerary/items', {
          params: { path: { tripId } },
          body
        })
      ),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.itineraryItems(tripId) });
      void queryClient.invalidateQueries({ queryKey: queryKeys.tripActivityFeed(tripId) });
    }
  });
}

export function useUpdateItineraryItemContent(tripId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async ({
      itemId,
      body,
      eTag
    }: {
      itemId: string;
      body: UpdateItineraryItemContentRequest;
      eTag: string;
    }) =>
      unwrap(
        await apiClient.PUT('/v1/trips/{tripId}/itinerary/items/{itemId}', {
          params: { path: { tripId, itemId } },
          headers: { 'If-Match': eTag },
          body
        })
      ),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.itineraryItems(tripId) });
      void queryClient.invalidateQueries({ queryKey: queryKeys.tripActivityFeed(tripId) });
    }
  });
}

export function useScheduleItineraryItem(tripId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async ({
      itemId,
      body,
      eTag
    }: {
      itemId: string;
      body: ScheduleItineraryItemRequest;
      eTag: string;
    }) =>
      unwrap(
        await apiClient.PUT('/v1/trips/{tripId}/itinerary/items/{itemId}/schedule', {
          params: { path: { tripId, itemId } },
          headers: { 'If-Match': eTag },
          body
        })
      ),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.itineraryItems(tripId) });
      void queryClient.invalidateQueries({ queryKey: queryKeys.tripActivityFeed(tripId) });
    }
  });
}

export function useUnscheduleItineraryItem(tripId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async ({ itemId, eTag }: { itemId: string; eTag: string }) =>
      unwrap(
        await apiClient.DELETE('/v1/trips/{tripId}/itinerary/items/{itemId}/schedule', {
          params: { path: { tripId, itemId } },
          headers: { 'If-Match': eTag }
        })
      ),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.itineraryItems(tripId) });
      void queryClient.invalidateQueries({ queryKey: queryKeys.tripActivityFeed(tripId) });
    }
  });
}

export function useSetItineraryApplicability(tripId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async ({
      itemId,
      body,
      eTag
    }: {
      itemId: string;
      body: SetApplicabilityRequest;
      eTag: string;
    }) =>
      unwrap(
        await apiClient.PUT('/v1/trips/{tripId}/itinerary/items/{itemId}/applicability', {
          params: { path: { tripId, itemId } },
          headers: { 'If-Match': eTag },
          body
        })
      ),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.itineraryItems(tripId) });
      void queryClient.invalidateQueries({ queryKey: queryKeys.tripActivityFeed(tripId) });
    }
  });
}

export function useDeleteItineraryItem(tripId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (itemId: string) => {
      const { error } = await apiClient.DELETE('/v1/trips/{tripId}/itinerary/items/{itemId}', {
        params: { path: { tripId, itemId } }
      });
      if (error) {
        throw error;
      }
    },
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.itineraryItems(tripId) });
      void queryClient.invalidateQueries({ queryKey: queryKeys.tripActivityFeed(tripId) });
    }
  });
}

// ---------- Comments ----------

export function useItineraryComments(tripId: string, itemId: string | undefined) {
  return useQuery({
    queryKey: queryKeys.itineraryComments(tripId, itemId ?? ''),
    queryFn: async () =>
      unwrap(
        await apiClient.GET('/v1/trips/{tripId}/itinerary/items/{itemId}/comments', {
          params: { path: { tripId, itemId: itemId! } }
        })
      ),
    enabled: Boolean(tripId && itemId)
  });
}

export function useAddItineraryComment(tripId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async ({ itemId, body }: { itemId: string; body: AddCommentRequest }) =>
      unwrap(
        await apiClient.POST('/v1/trips/{tripId}/itinerary/items/{itemId}/comments', {
          params: { path: { tripId, itemId } },
          body
        })
      ),
    onSuccess: (_data, variables) => {
      void queryClient.invalidateQueries({ queryKey: queryKeys.itineraryComments(tripId, variables.itemId) });
      void queryClient.invalidateQueries({ queryKey: queryKeys.tripActivityFeed(tripId) });
    }
  });
}

// ---------- Activity feed ----------

export function useTripActivityFeed(tripId: string) {
  return useQuery({
    queryKey: queryKeys.tripActivityFeed(tripId),
    queryFn: async () =>
      unwrap(
        await apiClient.GET('/v1/trips/{tripId}/itinerary/activity-feed', { params: { path: { tripId } } })
      ),
    enabled: Boolean(tripId)
  });
}

// ---------- Traveller filter ----------

export function useTravellerFilter(tripId: string) {
  return useQuery({
    queryKey: queryKeys.travellerFilter(tripId),
    queryFn: async () =>
      unwrap(
        await apiClient.GET('/v1/trips/{tripId}/itinerary/traveller-filter', { params: { path: { tripId } } })
      ),
    enabled: Boolean(tripId)
  });
}

export function useUpdateTravellerFilter(tripId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async ({ body, eTag }: { body: SetTravellerFilterRequest; eTag: string }) =>
      unwrap(
        await apiClient.PUT('/v1/trips/{tripId}/itinerary/traveller-filter', {
          params: { path: { tripId } },
          headers: { 'If-Match': eTag },
          body
        })
      ),
    onSuccess: (filter) => {
      queryClient.setQueryData(queryKeys.travellerFilter(tripId), filter);
      void queryClient.invalidateQueries({ queryKey: queryKeys.itineraryItems(tripId) });
    }
  });
}

export type { ItineraryItemResponse };
