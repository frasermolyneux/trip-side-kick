export const queryKeys = {
  trips: ['trips'] as const,
  trip: (tripId: string) => ['trips', tripId] as const,
  members: (tripId: string) => ['trips', tripId, 'members'] as const,
  invitations: (tripId: string) => ['trips', tripId, 'invitations'] as const,
  travellers: (tripId: string) => ['trips', tripId, 'travellers'] as const
};
