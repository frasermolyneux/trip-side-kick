using MX.TripSideKick.Domain.Common;
using MX.TripSideKick.Domain.Invitations;
using MX.TripSideKick.Domain.Memberships;
using MX.TripSideKick.Domain.Travellers;
using MX.TripSideKick.Domain.Trips;

using NodaTime;

namespace MX.TripSideKick.Web.Tests.Domain;

public sealed class InvitationTests
{
    private static readonly TripId AnyTripId = TripId.New();
    private static readonly Instant Now = SystemClock.Instance.GetCurrentInstant();

    [Fact]
    public void Create_requires_a_non_empty_invited_email()
    {
        Assert.Throws<ArgumentException>(() =>
            Invitation.Create(AnyTripId, string.Empty, MembershipRole.Editor, Now));
    }

    [Fact]
    public void Create_defaults_to_a_non_travelling_planner_when_no_link_kind_is_specified()
    {
        var invitation = Invitation.Create(AnyTripId, "person@example.test", MembershipRole.Viewer, Now);

        Assert.Equal(TravellerLinkKind.NonTravellingPlanner, invitation.LinkKind);
        Assert.Null(invitation.ExistingTravellerId);
        Assert.Null(invitation.NewTravellerDisplayName);
    }

    [Fact]
    public void Create_requires_an_existing_traveller_id_when_linking_to_an_existing_traveller()
    {
        Assert.Throws<ArgumentException>(() => Invitation.Create(
            AnyTripId, "person@example.test", MembershipRole.Editor, Now, TravellerLinkKind.ExistingTraveller));
    }

    [Fact]
    public void Create_requires_a_display_name_when_linking_to_a_new_traveller()
    {
        Assert.Throws<ArgumentException>(() => Invitation.Create(
            AnyTripId, "person@example.test", MembershipRole.Editor, Now, TravellerLinkKind.NewLinkedTraveller));
    }

    [Fact]
    public void Create_is_pending_with_a_freshly_generated_acceptance_token()
    {
        var invitation = Invitation.Create(AnyTripId, "person@example.test", MembershipRole.Editor, Now);

        Assert.Equal(InvitationStatus.Pending, invitation.Status);
        Assert.NotEqual(Guid.Empty, invitation.AcceptanceToken);
    }

    [Fact]
    public void Resend_generates_a_new_acceptance_token_so_the_old_link_stops_working()
    {
        var invitation = Invitation.Create(AnyTripId, "person@example.test", MembershipRole.Editor, Now);
        var originalToken = invitation.AcceptanceToken;

        invitation.Resend();

        Assert.NotEqual(originalToken, invitation.AcceptanceToken);
        Assert.Equal(InvitationStatus.Pending, invitation.Status);
    }

    [Fact]
    public void Resend_is_rejected_once_the_invitation_is_no_longer_pending()
    {
        var invitation = Invitation.Create(AnyTripId, "person@example.test", MembershipRole.Editor, Now);
        invitation.Revoke();

        Assert.Throws<InvitationStateException>(invitation.Resend);
    }

    [Fact]
    public void Revoke_transitions_a_pending_invitation_to_revoked()
    {
        var invitation = Invitation.Create(AnyTripId, "person@example.test", MembershipRole.Editor, Now);

        invitation.Revoke();

        Assert.Equal(InvitationStatus.Revoked, invitation.Status);
    }

    [Fact]
    public void Revoke_is_rejected_once_already_revoked()
    {
        var invitation = Invitation.Create(AnyTripId, "person@example.test", MembershipRole.Editor, Now);
        invitation.Revoke();

        Assert.Throws<InvitationStateException>(invitation.Revoke);
    }

    [Fact]
    public void EnsureCanBeAcceptedBy_succeeds_when_the_verified_email_matches_case_insensitively()
    {
        var invitation = Invitation.Create(AnyTripId, "Person@Example.test", MembershipRole.Editor, Now);

        var exception = Record.Exception(() => invitation.EnsureCanBeAcceptedBy("person@example.TEST"));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureCanBeAcceptedBy_rejects_a_mismatched_email()
    {
        var invitation = Invitation.Create(AnyTripId, "person@example.test", MembershipRole.Editor, Now);

        Assert.Throws<InvitationIdentityMismatchException>(() =>
            invitation.EnsureCanBeAcceptedBy("someone-else@example.test"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void EnsureCanBeAcceptedBy_rejects_a_missing_verified_email(string? verifiedEmail)
    {
        var invitation = Invitation.Create(AnyTripId, "person@example.test", MembershipRole.Editor, Now);

        Assert.Throws<InvitationIdentityMismatchException>(() => invitation.EnsureCanBeAcceptedBy(verifiedEmail));
    }

    [Fact]
    public void EnsureCanBeAcceptedBy_is_rejected_once_the_invitation_is_no_longer_pending()
    {
        var invitation = Invitation.Create(AnyTripId, "person@example.test", MembershipRole.Editor, Now);
        invitation.Revoke();

        Assert.Throws<InvitationStateException>(() => invitation.EnsureCanBeAcceptedBy("person@example.test"));
    }

    [Fact]
    public void MarkAccepted_transitions_to_accepted_and_records_the_timestamp()
    {
        var invitation = Invitation.Create(AnyTripId, "person@example.test", MembershipRole.Editor, Now);
        var acceptedAt = Now.Plus(Duration.FromMinutes(5));

        invitation.MarkAccepted(acceptedAt);

        Assert.Equal(InvitationStatus.Accepted, invitation.Status);
        Assert.Equal(acceptedAt, invitation.AcceptedAtUtc);
    }

    [Fact]
    public void Create_with_an_existing_traveller_link_records_the_traveller_id()
    {
        var travellerId = TravellerId.New();

        var invitation = Invitation.Create(
            AnyTripId, "person@example.test", MembershipRole.Editor, Now, TravellerLinkKind.ExistingTraveller, travellerId);

        Assert.Equal(TravellerLinkKind.ExistingTraveller, invitation.LinkKind);
        Assert.Equal(travellerId, invitation.ExistingTravellerId);
    }

    [Fact]
    public void Create_with_a_new_linked_traveller_records_the_display_name()
    {
        var invitation = Invitation.Create(
            AnyTripId, "person@example.test", MembershipRole.Editor, Now, TravellerLinkKind.NewLinkedTraveller,
            newTravellerDisplayName: "Jamie");

        Assert.Equal(TravellerLinkKind.NewLinkedTraveller, invitation.LinkKind);
        Assert.Equal("Jamie", invitation.NewTravellerDisplayName);
    }
}
