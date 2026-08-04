using System.Net;
using System.Net.Http.Json;

namespace MX.TripSideKick.Web.Tests.Hosting;

/// <summary>
/// Proves the BFF sign-in surface: reachable only on the app host, and <c>/v1/auth/me</c> reports
/// the real session state without ever returning a token.
/// </summary>
public sealed class AuthEndpointsTests(TripSideKickApplicationFactory factory)
    : IClassFixture<TripSideKickApplicationFactory>
{
    private readonly TripSideKickApplicationFactory factory = factory;

    [Fact]
    public async Task Me_reports_anonymous_when_there_is_no_session()
    {
        using var client = factory.CreateClientFor(TripSideKickApplicationFactory.AppHost);

        using var response = await client.GetAsync(new Uri("/v1/auth/me", UriKind.Relative));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"isAuthenticated\":false", body, StringComparison.Ordinal);
        Assert.Contains("\"displayName\":null", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Me_reports_the_signed_in_user_without_leaking_tokens()
    {
        using var client = factory.CreateAuthenticatedClientFor(
            TripSideKickApplicationFactory.AppHost,
            subjectId: "11111111-2222-3333-4444-555555555555",
            displayName: "Ada Lovelace");

        using var response = await client.GetAsync(new Uri("/v1/auth/me", UriKind.Relative));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"isAuthenticated\":true", body, StringComparison.Ordinal);
        Assert.Contains("\"displayName\":\"Ada Lovelace\"", body, StringComparison.Ordinal);
        Assert.DoesNotContain("access_token", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("id_token", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Status_reflects_the_authenticated_session()
    {
        using var client = factory.CreateAuthenticatedClientFor(
            TripSideKickApplicationFactory.AppHost,
            subjectId: "11111111-2222-3333-4444-555555555555");

        using var response = await client.GetAsync(new Uri("/v1/status", UriKind.Relative));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"authenticated\":true", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Logout_rejects_a_post_without_an_antiforgery_token()
    {
        using var client = factory.CreateClientFor(TripSideKickApplicationFactory.AppHost);

        using var response = await client.PostAsync(new Uri("/v1/auth/logout", UriKind.Relative), content: null);

        // A third-party page can't force a sign-out via a bare cross-site POST: without the
        // antiforgery cookie/header pair the request is rejected, not treated as a valid sign-out.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Logout_succeeds_when_the_antiforgery_token_is_echoed_back()
    {
        // HandleCookies defaults to true for WebApplicationFactory clients, so the antiforgery
        // cookie issued below is automatically attached to the follow-up POST on this same client.
        using var client = factory.CreateClientFor(TripSideKickApplicationFactory.AppHost);

        using var tokenResponse = await client.GetAsync(new Uri("/v1/auth/antiforgery", UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, tokenResponse.StatusCode);

        var tokenBody = await tokenResponse.Content.ReadFromJsonAsync<AntiforgeryTokenPayload>();
        Assert.NotNull(tokenBody);

        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri("/v1/auth/logout", UriKind.Relative));
        request.Headers.Add("X-CSRF-TOKEN", tokenBody!.Token);

        using var response = await client.SendAsync(request);

        Assert.NotEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private sealed record AntiforgeryTokenPayload(string Token);
}
