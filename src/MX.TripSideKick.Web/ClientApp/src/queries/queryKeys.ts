export const queryKeys = {
  trips: ['trips'] as const,
  trip: (tripId: string) => ['trips', tripId] as const,
  members: (tripId: string) => ['trips', tripId, 'members'] as const,
  invitations: (tripId: string) => ['trips', tripId, 'invitations'] as const,
  travellers: (tripId: string) => ['trips', tripId, 'travellers'] as const,
  itineraryItems: (tripId: string) => ['trips', tripId, 'itinerary', 'items'] as const,
  itineraryComments: (tripId: string, itemId: string) =>
    ['trips', tripId, 'itinerary', 'items', itemId, 'comments'] as const,
  tripActivityFeed: (tripId: string) => ['trips', tripId, 'itinerary', 'activity-feed'] as const,
  travellerFilter: (tripId: string) => ['trips', tripId, 'itinerary', 'traveller-filter'] as const
};
