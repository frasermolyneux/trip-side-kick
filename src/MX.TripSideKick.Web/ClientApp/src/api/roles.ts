/**
 * Mirrors `MembershipRole` (MX.TripSideKick.Domain.Memberships) and `TravellerLinkKind`
 * (MX.TripSideKick.Domain.Invitations) - both serialize as plain numbers over the wire (default
 * .NET enum JSON behaviour), so the values here must stay in sync with the C# enums.
 */
export const MembershipRole = {
  Viewer: 0,
  Editor: 1,
  Owner: 2
} as const;

export type MembershipRoleValue = (typeof MembershipRole)[keyof typeof MembershipRole];

export const membershipRoleLabels: Record<MembershipRoleValue, string> = {
  [MembershipRole.Viewer]: 'Viewer',
  [MembershipRole.Editor]: 'Editor',
  [MembershipRole.Owner]: 'Owner'
};

export const TravellerLinkKind = {
  NonTravellingPlanner: 0,
  ExistingTraveller: 1,
  NewLinkedTraveller: 2
} as const;

export type TravellerLinkKindValue = (typeof TravellerLinkKind)[keyof typeof TravellerLinkKind];

export const travellerLinkKindLabels: Record<TravellerLinkKindValue, string> = {
  [TravellerLinkKind.NonTravellingPlanner]: 'Non-travelling planner',
  [TravellerLinkKind.ExistingTraveller]: 'Existing traveller',
  [TravellerLinkKind.NewLinkedTraveller]: 'New traveller'
};

/** Wire values of `TripDates.Status` - see `TripDatesModel.From` in `TripsController`. */
export type TripDateStatus = 'undecided' | 'approximate' | 'confirmed';

/** Wire values of `Invitation.Status` - see `InvitationResponse.From` in `InvitationsController`. */
export type InvitationStatusValue = 'pending' | 'accepted' | 'revoked';
