import type { components } from './generated/schema';

export type TripResponse = components['schemas']['TripResponse'];
export type TripDatesModel = components['schemas']['TripDatesModel'];
export type CreateTripRequest = components['schemas']['CreateTripRequest'];
export type UpdateTripRequest = components['schemas']['UpdateTripRequest'];

export type MembershipResponse = components['schemas']['MembershipResponse'];
export type ChangeRoleRequest = components['schemas']['ChangeRoleRequest'];

export type InvitationResponse = components['schemas']['InvitationResponse'];
export type CreateInvitationRequest = components['schemas']['CreateInvitationRequest'];
export type AcceptInvitationRequest = components['schemas']['AcceptInvitationRequest'];

export type TravellerResponse = components['schemas']['TravellerResponse'];
export type LinkSelfAsTravellerRequest = components['schemas']['LinkSelfAsTravellerRequest'];

export type ItineraryItemResponse = components['schemas']['ItineraryItemResponse'];
export type ItineraryScheduleResponse = components['schemas']['ItineraryScheduleResponse'];
export type CreateItineraryItemRequest = components['schemas']['CreateItineraryItemRequest'];
export type UpdateItineraryItemContentRequest = components['schemas']['UpdateItineraryItemContentRequest'];
export type ScheduleItineraryItemRequest = components['schemas']['ScheduleItineraryItemRequest'];
export type SetApplicabilityRequest = components['schemas']['SetApplicabilityRequest'];
export type ItineraryCommentResponse = components['schemas']['ItineraryCommentResponse'];
export type AddCommentRequest = components['schemas']['AddCommentRequest'];
export type TripActivityFeedEntryResponse = components['schemas']['TripActivityFeedEntryResponse'];
export type TripTravellerFilterResponse = components['schemas']['TripTravellerFilterResponse'];
export type SetTravellerFilterRequest = components['schemas']['SetTravellerFilterRequest'];
