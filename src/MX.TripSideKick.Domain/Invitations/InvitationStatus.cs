namespace MX.TripSideKick.Domain.Invitations;

public enum InvitationStatus
{
    Pending = 0,
    Accepted = 1,
    Revoked = 2
}

/// <summary>
/// How an invitation connects to the trip's traveller list once accepted.
/// </summary>
public enum TravellerLinkKind
{
    /// <summary>The invitee gets access but is not added as a traveller (a "non-travelling planner").</summary>
    NonTravellingPlanner = 0,

    /// <summary>The invitee's new membership is linked to an existing, not-yet-linked traveller record.</summary>
    ExistingTraveller = 1,

    /// <summary>A new traveller record is created and linked to the invitee's new membership.</summary>
    NewLinkedTraveller = 2
}
