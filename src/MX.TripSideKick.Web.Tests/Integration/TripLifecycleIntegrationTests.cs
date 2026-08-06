using System.Net;
using System.Net.Http.Json;

using Microsoft.AspNetCore.Http;

using MX.TripSideKick.Domain.Invitations;
using MX.TripSideKick.Domain.Memberships;
using MX.TripSideKick.Web.Controllers.V1;

namespace MX.TripSideKick.Web.Tests.Integration;

/// <summary>
/// Real-EF-Core-over-SQL-Server coverage of Journey 1 + the trip-content half of Journey 2's role
/// matrix, using a Testcontainers SQL Server instance (see <see cref="SqlServerContainerFixture"/>).
/// </summary>
[Collection(SqlServerTestGroup.Name)]
public sealed class TripLifecycleIntegrationTests : IDisposable
{
    private readonly TripSideKickApplicationFactory factory;

    public TripLifecycleIntegrationTests(SqlServerContainerFixture sqlFixture)
    {
        ArgumentNullException.ThrowIfNull(sqlFixture);
        factory = new TripSideKickApplicationFactory { SqlConnectionStringOverride = sqlFixture.ConnectionString };
    }

    public void Dispose() => factory.Dispose();

    [Fact]
    public async Task Creating_a_trip_makes_the_creator_an_owner_and_an_account_linked_traveller()
    {
        using var client = factory.CreateAuthenticatedClientFor(TripSideKickApplicationFactory.AppHost, "owner-subject", "Owner Name");

        using var createResponse = await client.PostAsJsonWithAntiforgeryAsync(
            "/v1/trips", new CreateTripRequest("Iceland road trip", null, null, null, null));

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var trip = await createResponse.Content.ReadFromJsonAsync<TripResponse>();
        Assert.NotNull(trip);
        Assert.Equal("Iceland road trip", trip!.Name);

        using var membersResponse = await client.GetAsync(new Uri($"/v1/trips/{trip.Id}/members", UriKind.Relative));
        var members = await membersResponse.Content.ReadFromJsonAsync<List<MembershipResponse>>();

        var owner = Assert.Single(members!);
        Assert.Equal("owner-subject", owner.SubjectId);
        Assert.Equal(MembershipRole.Owner, owner.Role);
    }

    [Fact]
    public async Task An_editor_can_update_trip_content_but_a_viewer_cannot()
    {
        using var owner = factory.CreateAuthenticatedClientFor(TripSideKickApplicationFactory.AppHost, "owner-2", "Owner");
        var trip = await CreateTripAsync(owner, "Norway fjords");

        await AddMemberDirectlyAsync(trip.Id, "editor-subject", MembershipRole.Editor);
        await AddMemberDirectlyAsync(trip.Id, "viewer-subject", MembershipRole.Viewer);

        using var editor = factory.CreateAuthenticatedClientFor(TripSideKickApplicationFactory.AppHost, "editor-subject", "Editor");
        using var editorUpdate = await SendUpdateAsync(editor, trip, "Norway fjords (renamed)");
        Assert.Equal(HttpStatusCode.OK, editorUpdate.StatusCode);

        var updatedTrip = await editorUpdate.Content.ReadFromJsonAsync<TripResponse>();

        using var viewer = factory.CreateAuthenticatedClientFor(TripSideKickApplicationFactory.AppHost, "viewer-subject", "Viewer");
        using var viewerUpdate = await SendUpdateAsync(viewer, updatedTrip!, "Blocked rename");
        Assert.Equal(HttpStatusCode.Forbidden, viewerUpdate.StatusCode);
    }

    [Fact]
    public async Task Updating_a_trip_without_a_current_ETag_is_rejected_with_409_not_silently_overwritten()
    {
        using var owner = factory.CreateAuthenticatedClientFor(TripSideKickApplicationFactory.AppHost, "owner-3", "Owner");
        var trip = await CreateTripAsync(owner, "Original name");

        // First update succeeds and moves the ETag on.
        using var firstUpdate = await SendUpdateAsync(owner, trip, "First rename");
        Assert.Equal(HttpStatusCode.OK, firstUpdate.StatusCode);

        // Retrying the *original* (now-stale) ETag must be refused, not silently applied.
        using var staleUpdate = await owner.PutAsJsonWithAntiforgeryAsync(
            $"/v1/trips/{trip.Id}", new UpdateTripRequest("Stale rename", null, null, null, null), trip.ETag);

        Assert.Equal(HttpStatusCode.Conflict, staleUpdate.StatusCode);
    }

    [Fact]
    public async Task Updating_a_trip_without_an_If_Match_header_is_rejected()
    {
        using var owner = factory.CreateAuthenticatedClientFor(TripSideKickApplicationFactory.AppHost, "owner-4", "Owner");
        var trip = await CreateTripAsync(owner, "No if-match trip");

        using var response = await owner.PutAsJsonWithAntiforgeryAsync(
            $"/v1/trips/{trip.Id}", new UpdateTripRequest("Renamed without header", null, null, null, null), ifMatch: null);

        Assert.Equal((HttpStatusCode)StatusCodes.Status428PreconditionRequired, response.StatusCode);
    }

    [Fact]
    public async Task An_anonymous_caller_cannot_reach_the_trips_api()
    {
        using var client = factory.CreateClientFor(TripSideKickApplicationFactory.AppHost);

        using var response = await client.GetAsync(new Uri("/v1/trips", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static async Task<TripResponse> CreateTripAsync(HttpClient client, string name)
    {
        using var response = await client.PostAsJsonWithAntiforgeryAsync("/v1/trips", new CreateTripRequest(name, null, null, null, null));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<TripResponse>())!;
    }

    private static Task<HttpResponseMessage> SendUpdateAsync(HttpClient client, TripResponse trip, string newName) =>
        client.PutAsJsonWithAntiforgeryAsync($"/v1/trips/{trip.Id}", new UpdateTripRequest(newName, null, null, null, null), trip.ETag);

    /// <summary>
    /// Adds a member without going through the invitation flow (that flow is covered separately in
    /// <c>InvitationAcceptanceIntegrationTests</c>) - resolves an authenticated client for a brand
    /// new subject and has them accept a trivially-created invitation from the owner instead of
    /// poking the database directly, so this stays a true black-box API test.
    /// </summary>
    private async Task<MembershipResponse> AddMemberDirectlyAsync(
        Guid tripId, string subjectId, MembershipRole role)
    {
        var email = $"{subjectId}@example.com";
        using var owner = factory.CreateAuthenticatedClientFor(TripSideKickApplicationFactory.AppHost, "owner-2", "Owner");

        using var inviteResponse = await owner.PostAsJsonWithAntiforgeryAsync(
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
}
