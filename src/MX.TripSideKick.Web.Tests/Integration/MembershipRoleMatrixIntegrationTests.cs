using System.Net;
using System.Net.Http.Json;

using MX.TripSideKick.Domain.Invitations;
using MX.TripSideKick.Domain.Memberships;
using MX.TripSideKick.Web.Controllers.V1;

namespace MX.TripSideKick.Web.Tests.Integration;

/// <summary>
/// Real-EF-Core-over-SQL-Server coverage of Journey 2's role matrix and last-owner protection.
/// </summary>
[Collection(SqlServerTestGroup.Name)]
public sealed class MembershipRoleMatrixIntegrationTests : IDisposable
{
    private readonly TripSideKickApplicationFactory factory;

    public MembershipRoleMatrixIntegrationTests(SqlServerContainerFixture sqlFixture)
    {
        ArgumentNullException.ThrowIfNull(sqlFixture);
        factory = new TripSideKickApplicationFactory { SqlConnectionStringOverride = sqlFixture.ConnectionString };
    }

    public void Dispose() => factory.Dispose();

    [Fact]
    public async Task Only_owners_can_change_a_members_role()
    {
        using var owner = factory.CreateAuthenticatedClientFor(TripSideKickApplicationFactory.AppHost, "owner-a", "Owner");
        var (tripId, _) = await CreateTripAsync(owner, "Role matrix trip A");
        var editor = await InviteAndAcceptAsync(owner, tripId, "editor-a", MembershipRole.Editor);

        using var editorClient = factory.CreateAuthenticatedClientFor(TripSideKickApplicationFactory.AppHost, "editor-a", "Editor");
        using var response = await SendChangeRoleAsync(editorClient, tripId, editor.Id, editor.ETag, MembershipRole.Viewer);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task The_last_owner_cannot_be_demoted()
    {
        using var owner = factory.CreateAuthenticatedClientFor(TripSideKickApplicationFactory.AppHost, "owner-b", "Owner");
        var (tripId, ownerMembership) = await CreateTripAsync(owner, "Role matrix trip B");

        using var response = await SendChangeRoleAsync(owner, tripId, ownerMembership.Id, ownerMembership.ETag, MembershipRole.Editor);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task The_last_owner_cannot_be_removed_or_leave()
    {
        using var owner = factory.CreateAuthenticatedClientFor(TripSideKickApplicationFactory.AppHost, "owner-c", "Owner");
        var (tripId, ownerMembership) = await CreateTripAsync(owner, "Role matrix trip C");

        using var removeResponse = await owner.DeleteWithAntiforgeryAsync($"/v1/trips/{tripId}/members/{ownerMembership.Id}");
        Assert.Equal(HttpStatusCode.Conflict, removeResponse.StatusCode);

        using var leaveResponse = await owner.PostWithAntiforgeryAsync($"/v1/trips/{tripId}/members/leave");
        Assert.Equal(HttpStatusCode.Conflict, leaveResponse.StatusCode);
    }

    [Fact]
    public async Task Demoting_an_owner_is_allowed_once_a_second_owner_exists()
    {
        using var owner = factory.CreateAuthenticatedClientFor(TripSideKickApplicationFactory.AppHost, "owner-d1", "Owner 1");
        var (tripId, ownerMembership) = await CreateTripAsync(owner, "Role matrix trip D");
        await InviteAndAcceptAsync(owner, tripId, "owner-d2", MembershipRole.Owner);

        using var response = await SendChangeRoleAsync(owner, tripId, ownerMembership.Id, ownerMembership.ETag, MembershipRole.Editor);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<MembershipResponse>();
        Assert.Equal(MembershipRole.Editor, updated!.Role);
    }

    [Fact]
    public async Task A_viewer_can_see_membership_but_cannot_remove_anyone()
    {
        using var owner = factory.CreateAuthenticatedClientFor(TripSideKickApplicationFactory.AppHost, "owner-e", "Owner");
        var (tripId, _) = await CreateTripAsync(owner, "Role matrix trip E");
        var viewer = await InviteAndAcceptAsync(owner, tripId, "viewer-e", MembershipRole.Viewer);

        using var viewerClient = factory.CreateAuthenticatedClientFor(TripSideKickApplicationFactory.AppHost, "viewer-e", "Viewer");

        using var listResponse = await viewerClient.GetAsync(new Uri($"/v1/trips/{tripId}/members", UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

        using var removeResponse = await viewerClient.DeleteWithAntiforgeryAsync($"/v1/trips/{tripId}/members/{viewer.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, removeResponse.StatusCode);
    }

    private static async Task<(Guid TripId, MembershipResponse Owner)> CreateTripAsync(HttpClient ownerClient, string name)
    {
        using var createResponse = await ownerClient.PostAsJsonWithAntiforgeryAsync(
            "/v1/trips", new CreateTripRequest(name, null, null, null, null));
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var trip = await createResponse.Content.ReadFromJsonAsync<TripResponse>();

        using var membersResponse = await ownerClient.GetAsync(new Uri($"/v1/trips/{trip!.Id}/members", UriKind.Relative));
        var members = await membersResponse.Content.ReadFromJsonAsync<List<MembershipResponse>>();

        return (trip.Id, Assert.Single(members!));
    }

    private async Task<MembershipResponse> InviteAndAcceptAsync(HttpClient ownerClient, Guid tripId, string subjectId, MembershipRole role)
    {
        var email = $"{subjectId}@example.com";

        using var inviteResponse = await ownerClient.PostAsJsonWithAntiforgeryAsync(
            $"/v1/trips/{tripId}/invitations",
            new CreateInvitationRequest(email, role, TravellerLinkKind.NonTravellingPlanner, null, null));
        Assert.Equal(HttpStatusCode.Created, inviteResponse.StatusCode);
        var invitation = await inviteResponse.Content.ReadFromJsonAsync<InvitationResponse>();

        using var invitee = factory.CreateAuthenticatedClientFor(TripSideKickApplicationFactory.AppHost, subjectId, subjectId, email);
        using var acceptResponse = await invitee.PostAsJsonWithAntiforgeryAsync(
            "/v1/invitations/accept", new AcceptInvitationRequest(AcceptanceUrls.ExtractToken(invitation!.AcceptanceUrl)));
        Assert.Equal(HttpStatusCode.OK, acceptResponse.StatusCode);

        return (await acceptResponse.Content.ReadFromJsonAsync<MembershipResponse>())!;
    }

    private static async Task<HttpResponseMessage> SendChangeRoleAsync(
        HttpClient client, Guid tripId, Guid membershipId, string eTag, MembershipRole newRole)
    {
        var token = await client.GetAsync(new Uri("/v1/auth/antiforgery", UriKind.Relative));
        var payload = await token.Content.ReadFromJsonAsync<AntiforgeryTokenPayload>();

        var request = new HttpRequestMessage(HttpMethod.Put, $"/v1/trips/{tripId}/members/{membershipId}/role")
        {
            Content = JsonContent.Create(new ChangeRoleRequest(newRole))
        };
        request.Headers.TryAddWithoutValidation("If-Match", eTag);
        request.Headers.Add("X-CSRF-TOKEN", payload!.Token);
        return await client.SendAsync(request);
    }

    private sealed record AntiforgeryTokenPayload(string Token);
}
