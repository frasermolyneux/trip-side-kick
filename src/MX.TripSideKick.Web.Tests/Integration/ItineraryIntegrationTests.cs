using System.Net;
using System.Net.Http.Json;

using MX.TripSideKick.Domain.Invitations;
using MX.TripSideKick.Domain.Memberships;
using MX.TripSideKick.Web.Controllers.V1;

namespace MX.TripSideKick.Web.Tests.Integration;

/// <summary>
/// Real-EF-Core-over-SQL-Server coverage of Journey 5's itinerary + traveller-filter surface:
/// role matrix, ETag/If-Match, traveller applicability + filter round-trips, and the collaborative
/// activity feed. Mirrors the shape of <see cref="MembershipRoleMatrixIntegrationTests"/>.
/// </summary>
[Collection(SqlServerTestGroup.Name)]
public sealed class ItineraryIntegrationTests : IDisposable
{
    private readonly TripSideKickApplicationFactory factory;

    public ItineraryIntegrationTests(SqlServerContainerFixture sqlFixture)
    {
        ArgumentNullException.ThrowIfNull(sqlFixture);
        factory = new TripSideKickApplicationFactory { SqlConnectionStringOverride = sqlFixture.ConnectionString };
    }

    public void Dispose() => factory.Dispose();

    [Fact]
    public async Task Editor_can_create_an_idea_and_Viewer_can_read_it_but_not_create()
    {
        using var owner = factory.CreateAuthenticatedClientFor(TripSideKickApplicationFactory.AppHost, "owner-i1", "Owner");
        var (tripId, _) = await CreateTripAsync(owner, "Itinerary matrix trip 1");
        await InviteAndAcceptAsync(owner, tripId, "editor-i1", MembershipRole.Editor);
        await InviteAndAcceptAsync(owner, tripId, "viewer-i1", MembershipRole.Viewer);

        using var editor = factory.CreateAuthenticatedClientFor(TripSideKickApplicationFactory.AppHost, "editor-i1", "Editor");
        using var viewer = factory.CreateAuthenticatedClientFor(TripSideKickApplicationFactory.AppHost, "viewer-i1", "Viewer");

        using var createResponse = await editor.PostAsJsonWithAntiforgeryAsync(
            $"/v1/trips/{tripId}/itinerary/items",
            new CreateItineraryItemRequest("Ride the funicular", null, "Bergen", null));
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        using var viewerCreateResponse = await viewer.PostAsJsonWithAntiforgeryAsync(
            $"/v1/trips/{tripId}/itinerary/items",
            new CreateItineraryItemRequest("Sneaky idea", null, null, null));
        Assert.Equal(HttpStatusCode.Forbidden, viewerCreateResponse.StatusCode);

        using var listResponse = await viewer.GetAsync(new Uri($"/v1/trips/{tripId}/itinerary/items", UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var items = await listResponse.Content.ReadFromJsonAsync<List<ItineraryItemResponse>>();
        Assert.Single(items!);
    }

    [Fact]
    public async Task Scheduling_requires_confirmed_trip_dates()
    {
        using var owner = factory.CreateAuthenticatedClientFor(TripSideKickApplicationFactory.AppHost, "owner-i2", "Owner");
        var (tripId, _) = await CreateTripAsync(owner, "Itinerary matrix trip 2");

        var item = await CreateIdeaAsync(owner, tripId, "Museum");

        using var scheduleResponse = await owner.PutAsJsonWithAntiforgeryAsync(
            $"/v1/trips/{tripId}/itinerary/items/{item.Id}/schedule",
            new ScheduleItineraryItemRequest(new DateOnly(2025, 6, 1), null, null),
            ifMatch: item.ETag);

        Assert.Equal(HttpStatusCode.Conflict, scheduleResponse.StatusCode);
    }

    [Fact]
    public async Task Scheduling_succeeds_and_ETag_prevents_stale_updates()
    {
        using var owner = factory.CreateAuthenticatedClientFor(TripSideKickApplicationFactory.AppHost, "owner-i3", "Owner");
        var (tripId, _) = await CreateTripAsync(owner, "Itinerary matrix trip 3", confirmedStart: new DateOnly(2025, 6, 1), confirmedEnd: new DateOnly(2025, 6, 10));

        var item = await CreateIdeaAsync(owner, tripId, "Museum");

        using var scheduleResponse = await owner.PutAsJsonWithAntiforgeryAsync(
            $"/v1/trips/{tripId}/itinerary/items/{item.Id}/schedule",
            new ScheduleItineraryItemRequest(new DateOnly(2025, 6, 3), new TimeOnly(10, 0), new TimeOnly(12, 0)),
            ifMatch: item.ETag);
        Assert.Equal(HttpStatusCode.OK, scheduleResponse.StatusCode);
        var scheduled = (await scheduleResponse.Content.ReadFromJsonAsync<ItineraryItemResponse>())!;
        Assert.Equal("scheduled", scheduled.Schedule.Status);

        // Reusing the stale ETag must now 409.
        using var staleResponse = await owner.PutAsJsonWithAntiforgeryAsync(
            $"/v1/trips/{tripId}/itinerary/items/{item.Id}",
            new UpdateItineraryItemContentRequest("Museum (renamed)", null, null),
            ifMatch: item.ETag);
        Assert.Equal(HttpStatusCode.Conflict, staleResponse.StatusCode);
    }

    [Fact]
    public async Task Applicability_and_traveller_filter_round_trip_and_control_visibility()
    {
        using var owner = factory.CreateAuthenticatedClientFor(TripSideKickApplicationFactory.AppHost, "owner-i4", "Owner");
        var (tripId, _) = await CreateTripAsync(owner, "Itinerary matrix trip 4");
        var editor = await InviteAndAcceptAsync(owner, tripId, "editor-i4", MembershipRole.Editor);

        // Link the editor as a traveller on the trip so we have two distinct traveller ids to
        // demonstrate applicability filtering.
        using var editorClient = factory.CreateAuthenticatedClientFor(TripSideKickApplicationFactory.AppHost, "editor-i4", "Editor");
        using var linkResponse = await editorClient.PostAsJsonWithAntiforgeryAsync(
            $"/v1/trips/{tripId}/travellers/self", new LinkSelfAsTravellerRequest(null));
        Assert.Equal(HttpStatusCode.OK, linkResponse.StatusCode);
        var editorTraveller = (await linkResponse.Content.ReadFromJsonAsync<TravellerResponse>())!;

        var everyoneItem = await CreateIdeaAsync(owner, tripId, "Group hike");
        var editorItem = await CreateIdeaAsync(owner, tripId, "Editor-only workshop");

        // Restrict "Editor-only workshop" to the editor's traveller row.
        using var applicabilityResponse = await owner.PutAsJsonWithAntiforgeryAsync(
            $"/v1/trips/{tripId}/itinerary/items/{editorItem.Id}/applicability",
            new SetApplicabilityRequest(new[] { editorTraveller.Id }),
            ifMatch: editorItem.ETag);
        Assert.Equal(HttpStatusCode.OK, applicabilityResponse.StatusCode);

        // Owner (whose linked traveller isn't in the "editor-only" list) switches to Me mode -
        // they should now only see the everyone-applicable item, not the editor-only one.
        using var filterGetResponse = await owner.GetAsync(new Uri($"/v1/trips/{tripId}/itinerary/traveller-filter", UriKind.Relative));
        var filter = (await filterGetResponse.Content.ReadFromJsonAsync<TripTravellerFilterResponse>())!;

        using var filterPutResponse = await owner.PutAsJsonWithAntiforgeryAsync(
            $"/v1/trips/{tripId}/itinerary/traveller-filter",
            new SetTravellerFilterRequest("me", null),
            ifMatch: filter.ETag);
        Assert.Equal(HttpStatusCode.OK, filterPutResponse.StatusCode);

        using var listResponse = await owner.GetAsync(new Uri($"/v1/trips/{tripId}/itinerary/items", UriKind.Relative));
        var items = await listResponse.Content.ReadFromJsonAsync<List<ItineraryItemResponse>>();

        Assert.Contains(items!, i => i.Id == everyoneItem.Id);
        Assert.DoesNotContain(items!, i => i.Id == editorItem.Id);
    }

    [Fact]
    public async Task Viewer_can_add_a_comment_and_it_appears_in_the_activity_feed()
    {
        using var owner = factory.CreateAuthenticatedClientFor(TripSideKickApplicationFactory.AppHost, "owner-i5", "Owner");
        var (tripId, _) = await CreateTripAsync(owner, "Itinerary matrix trip 5");
        await InviteAndAcceptAsync(owner, tripId, "viewer-i5", MembershipRole.Viewer);

        var item = await CreateIdeaAsync(owner, tripId, "Kayak tour");

        using var viewer = factory.CreateAuthenticatedClientFor(TripSideKickApplicationFactory.AppHost, "viewer-i5", "Viewer");

        using var commentResponse = await viewer.PostAsJsonWithAntiforgeryAsync(
            $"/v1/trips/{tripId}/itinerary/items/{item.Id}/comments",
            new AddCommentRequest("Great pick!"));
        Assert.Equal(HttpStatusCode.Created, commentResponse.StatusCode);

        using var deleteResponse = await viewer.DeleteWithAntiforgeryAsync($"/v1/trips/{tripId}/itinerary/items/{item.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, deleteResponse.StatusCode);

        using var feedResponse = await owner.GetAsync(new Uri($"/v1/trips/{tripId}/itinerary/activity-feed", UriKind.Relative));
        var feed = (await feedResponse.Content.ReadFromJsonAsync<List<TripActivityFeedEntryResponse>>())!;
        Assert.Contains(feed, e => e.EventType == "CommentAdded");
        Assert.Contains(feed, e => e.EventType == "ItemCreated");
    }

    [Fact]
    public async Task Anonymous_callers_get_401_on_the_itinerary_surface()
    {
        using var anonymous = factory.CreateClientFor(TripSideKickApplicationFactory.AppHost);

        using var response = await anonymous.GetAsync(new Uri($"/v1/trips/{Guid.NewGuid()}/itinerary/items", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static async Task<(Guid TripId, MembershipResponse Owner)> CreateTripAsync(
        HttpClient ownerClient, string name, DateOnly? confirmedStart = null, DateOnly? confirmedEnd = null)
    {
        TripDatesModel? dates = confirmedStart is { } start && confirmedEnd is { } end
            ? new TripDatesModel("confirmed", start, end)
            : null;

        using var createResponse = await ownerClient.PostAsJsonWithAntiforgeryAsync(
            "/v1/trips", new CreateTripRequest(name, null, null, dates, null));
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var trip = await createResponse.Content.ReadFromJsonAsync<TripResponse>();

        using var membersResponse = await ownerClient.GetAsync(new Uri($"/v1/trips/{trip!.Id}/members", UriKind.Relative));
        var members = await membersResponse.Content.ReadFromJsonAsync<List<MembershipResponse>>();
        return (trip.Id, Assert.Single(members!));
    }

    private async Task<MembershipResponse> InviteAndAcceptAsync(
        HttpClient ownerClient, Guid tripId, string subjectId, MembershipRole role)
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

    private static async Task<ItineraryItemResponse> CreateIdeaAsync(HttpClient client, Guid tripId, string title)
    {
        using var response = await client.PostAsJsonWithAntiforgeryAsync(
            $"/v1/trips/{tripId}/itinerary/items",
            new CreateItineraryItemRequest(title, null, null, null));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ItineraryItemResponse>())!;
    }
}
