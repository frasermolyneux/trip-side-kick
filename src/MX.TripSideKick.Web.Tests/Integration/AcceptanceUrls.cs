using Microsoft.AspNetCore.WebUtilities;

namespace MX.TripSideKick.Web.Tests.Integration;

/// <summary>Shared helper for parsing the stubbed <c>token</c> query parameter out of an invitation acceptance URL in tests.</summary>
internal static class AcceptanceUrls
{
    public static Guid ExtractToken(string acceptanceUrl)
    {
        var query = QueryHelpers.ParseQuery(new Uri(acceptanceUrl).Query);
        return Guid.Parse(query["token"].ToString());
    }
}
