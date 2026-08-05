using System.Net.Http.Json;

namespace MX.TripSideKick.Web.Tests.Integration;

/// <summary>
/// Test-only helper that fetches an antiforgery token from <c>GET /v1/auth/antiforgery</c> and
/// echoes it back via the <c>X-CSRF-TOKEN</c> header, mirroring what the real ClientApp's
/// <c>apiClient</c> middleware does for every mutating request. The Journey 1/2 endpoints
/// exercised by these integration tests are guarded by <c>[ValidateAntiForgeryToken]</c> (see
/// <c>AuthController</c> for the reference pattern), so a bare POST without this header is
/// rejected the same way a forged cross-site request would be - see
/// <see cref="Hosting.AuthEndpointsTests"/> for the equivalent proof against
/// <c>/v1/auth/logout</c>.
/// </summary>
internal static class AntiforgeryHttpClientExtensions
{
    public static async Task<HttpResponseMessage> PostAsJsonWithAntiforgeryAsync<TValue>(
        this HttpClient client, string requestUri, TValue value)
    {
        var token = await client.FetchAntiforgeryTokenAsync();
        using var request = new HttpRequestMessage(HttpMethod.Post, requestUri)
        {
            Content = JsonContent.Create(value)
        };
        request.Headers.Add("X-CSRF-TOKEN", token);
        return await client.SendAsync(request);
    }

    public static async Task<HttpResponseMessage> PostWithAntiforgeryAsync(this HttpClient client, string requestUri)
    {
        var token = await client.FetchAntiforgeryTokenAsync();
        using var request = new HttpRequestMessage(HttpMethod.Post, requestUri);
        request.Headers.Add("X-CSRF-TOKEN", token);
        return await client.SendAsync(request);
    }

    private static async Task<string> FetchAntiforgeryTokenAsync(this HttpClient client)
    {
        using var tokenResponse = await client.GetAsync(new Uri("/v1/auth/antiforgery", UriKind.Relative));
        tokenResponse.EnsureSuccessStatusCode();

        var payload = await tokenResponse.Content.ReadFromJsonAsync<AntiforgeryTokenPayload>();
        return payload!.Token;
    }

    private sealed record AntiforgeryTokenPayload(string Token);
}
