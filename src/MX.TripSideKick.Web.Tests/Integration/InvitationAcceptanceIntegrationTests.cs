using System.Net;
using System.Net.Http.Json;

using MX.TripSideKick.Domain.Memberships;
using MX.TripSideKick.Domain.Invitations;
using MX.TripSideKick.Web.Controllers.V1;

namespace MX.TripSideKick.Web.Tests.Integration;

/// <summary>
/// Real-EF-Core-over-SQL-Server coverage of Journey 2's invitation acceptance rules: email
/// binding, identity-mismatch rejection, and traveller linkage without duplication.
/// </summary>
[Collection(SqlServerTestGroup.Name)]
public sealed class InvitationAcceptanceIntegrationTests : IDisposable
{
    private readonly TripSideKickApplicationFactory factory;

    public InvitationAcceptanceIntegrationTests(SqlServerContainerFixture sqlFixture)
    {
        ArgumentNullException.ThrowIfNull(sqlFixture);
        factory = new TripSideKickApplicationFactory { SqlConnectionStringOverride = sqlFixture.ConnectionString };
    }

    public void Dispose() => factory.Dispose();

    [Fact]
    public async Task An_invite_cannot_be_accepted_by_a_mismatched_identity()
    {
        using var owner = factory.CreateAuthenticatedClientFor(TripSideKickApplicationFactory.AppHost, "owner-f", "Owner");
        var tripId = await CreateTripAsync(owner, "Invite mismatch trip");
        var invitation = await CreateInvitationAsync(owner, tripId, "intended@example.com", MembershipRole.Editor);

        using var impostor = factory.CreateAuthenticatedClientFor(
            TripSideKickApplicationFactory.AppHost, "impostor-subject", "Impostor", "someone-else@example.com");

        using var response = await impostor.PostAsJsonAsync(
            "/v1/invitations/accept", new AcceptInvitationRequest(AcceptanceUrls.ExtractToken(invitation.AcceptanceUrl)));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task An_invite_is_accepted_when_the_verified_email_matches_exactly()
    {
        using var owner = factory.CreateAuthenticatedClientFor(TripSideKickApplicationFactory.AppHost, "owner-g", "Owner");
        var tripId = await CreateTripAsync(owner, "Invite match trip");
        var invitation = await CreateInvitationAsync(owner, tripId, "friend@example.com", MembershipRole.Viewer);

        using var invitee = factory.CreateAuthenticatedClientFor(
            TripSideKickApplicationFactory.AppHost, "friend-subject", "Friend", "friend@example.com");

        using var response = await invitee.PostAsJsonAsync(
            "/v1/invitations/accept", new AcceptInvitationRequest(AcceptanceUrls.ExtractToken(invitation.AcceptanceUrl)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var membership = await response.Content.ReadFromJsonAsync<MembershipResponse>();
        Assert.Equal(MembershipRole.Viewer, membership!.Role);
        Assert.Equal("friend-subject", membership.SubjectId);
    }

    [Fact]
    public async Task Accepting_an_invite_with_a_new_linked_traveller_creates_exactly_one_new_traveller_record()
    {
        using var owner = factory.CreateAuthenticatedClientFor(TripSideKickApplicationFactory.AppHost, "owner-h", "Owner");
        var tripId = await CreateTripAsync(owner, "Invite linked traveller trip");

        using var travellersBefore = await owner.GetAsync(new Uri($"/v1/trips/{tripId}/travellers", UriKind.Relative));
        var beforeCount = (await travellersBefore.Content.ReadFromJsonAsync<List<TravellerResponse>>())!.Count;

        using var inviteResponse = await owner.PostAsJsonAsync(
            $"/v1/trips/{tripId}/invitations",
            new CreateInvitationRequest(
                "linked@example.com", MembershipRole.Editor, TravellerLinkKind.NewLinkedTraveller, null, "New Linked Traveller"));
        Assert.Equal(HttpStatusCode.Created, inviteResponse.StatusCode);
        var invitation = await inviteResponse.Content.ReadFromJsonAsync<InvitationResponse>();

        using var invitee = factory.CreateAuthenticatedClientFor(
            TripSideKickApplicationFactory.AppHost, "linked-subject", "Linked", "linked@example.com");
        using var acceptResponse = await invitee.PostAsJsonAsync(
            "/v1/invitations/accept", new AcceptInvitationRequest(AcceptanceUrls.ExtractToken(invitation!.AcceptanceUrl)));
        Assert.Equal(HttpStatusCode.OK, acceptResponse.StatusCode);
        var membership = await acceptResponse.Content.ReadFromJsonAsync<MembershipResponse>();

        using var travellersAfter = await owner.GetAsync(new Uri($"/v1/trips/{tripId}/travellers", UriKind.Relative));
        var travellers = await travellersAfter.Content.ReadFromJsonAsync<List<TravellerResponse>>();

        // Exactly one new traveller was created for the invitee - no duplicate traveller record.
        Assert.Equal(beforeCount + 1, travellers!.Count);
        var linkedTraveller = Assert.Single(travellers, t => t.DisplayName == "New Linked Traveller");
        Assert.Equal(membership!.Id, linkedTraveller.LinkedMembershipId);
    }

    [Fact]
    public async Task Accepting_an_invite_when_already_a_member_is_rejected()
    {
        using var owner = factory.CreateAuthenticatedClientFor(TripSideKickApplicationFactory.AppHost, "owner-i", "Owner");
        var tripId = await CreateTripAsync(owner, "Already member trip");

        var firstInvitation = await CreateInvitationAsync(owner, tripId, "twice@example.com", MembershipRole.Viewer);
        using var invitee = factory.CreateAuthenticatedClientFor(
            TripSideKickApplicationFactory.AppHost, "twice-subject", "Twice", "twice@example.com");
        using var firstAccept = await invitee.PostAsJsonAsync(
            "/v1/invitations/accept", new AcceptInvitationRequest(AcceptanceUrls.ExtractToken(firstInvitation.AcceptanceUrl)));
        Assert.Equal(HttpStatusCode.OK, firstAccept.StatusCode);

        var secondInvitation = await CreateInvitationAsync(owner, tripId, "twice@example.com", MembershipRole.Editor);
        using var secondAccept = await invitee.PostAsJsonAsync(
            "/v1/invitations/accept", new AcceptInvitationRequest(AcceptanceUrls.ExtractToken(secondInvitation.AcceptanceUrl)));

        Assert.Equal(HttpStatusCode.Conflict, secondAccept.StatusCode);
    }

    [Fact]
    public async Task A_revoked_invitation_cannot_be_accepted()
    {
        using var owner = factory.CreateAuthenticatedClientFor(TripSideKickApplicationFactory.AppHost, "owner-j", "Owner");
        var tripId = await CreateTripAsync(owner, "Revoked invite trip");
        var invitation = await CreateInvitationAsync(owner, tripId, "revoked@example.com", MembershipRole.Viewer);

        using var revokeResponse = await owner.PostAsync(
            new Uri($"/v1/trips/{tripId}/invitations/{invitation.Id}/revoke", UriKind.Relative), content: null);
        Assert.Equal(HttpStatusCode.NoContent, revokeResponse.StatusCode);

        using var invitee = factory.CreateAuthenticatedClientFor(
            TripSideKickApplicationFactory.AppHost, "revoked-subject", "Revoked", "revoked@example.com");
        using var acceptResponse = await invitee.PostAsJsonAsync(
            "/v1/invitations/accept", new AcceptInvitationRequest(AcceptanceUrls.ExtractToken(invitation.AcceptanceUrl)));

        Assert.Equal(HttpStatusCode.Conflict, acceptResponse.StatusCode);
    }

    private static async Task<Guid> CreateTripAsync(HttpClient ownerClient, string name)
    {
        using var response = await ownerClient.PostAsJsonAsync("/v1/trips", new CreateTripRequest(name, null, null, null, null));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var trip = await response.Content.ReadFromJsonAsync<TripResponse>();
        return trip!.Id;
    }

    private static async Task<InvitationResponse> CreateInvitationAsync(
        HttpClient ownerClient, Guid tripId, string invitedEmail, MembershipRole role)
    {
        using var response = await ownerClient.PostAsJsonAsync(
            $"/v1/trips/{tripId}/invitations",
            new CreateInvitationRequest(invitedEmail, role, TravellerLinkKind.NonTravellingPlanner, null, null));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<InvitationResponse>())!;
    }
}
