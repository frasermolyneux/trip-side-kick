using Microsoft.AspNetCore.Mvc;

namespace MX.TripSideKick.Web.Api;

/// <summary>
/// Shared helpers for mapping a SQL <c>rowversion</c>/<see cref="byte"/>[] to and from HTTP
/// <c>ETag</c>/<c>If-Match</c> header values, so every trip-scoped controller enforces optimistic
/// concurrency the same way.
/// </summary>
public static class RowVersionETag
{
    /// <summary>Formats a rowversion as a strong <c>ETag</c> header value (quoted base64).</summary>
    public static string ToETag(byte[]? rowVersion) =>
        $"\"{Convert.ToBase64String(rowVersion ?? [])}\"";

    /// <summary>
    /// Requires an <c>If-Match</c> header and parses it back into the rowversion bytes it encodes.
    /// Returns a <see cref="ProblemDetails"/> failure result when the header is missing or
    /// malformed - callers can propagate that failure straight back to the client.
    /// </summary>
    public static bool TryRequireIfMatch(HttpRequest request, out byte[] rowVersion, out ObjectResult? failure)
    {
        ArgumentNullException.ThrowIfNull(request);

        var headerValue = request.Headers.IfMatch.ToString();

        if (string.IsNullOrWhiteSpace(headerValue))
        {
            rowVersion = [];
            failure = new ObjectResult(new ProblemDetails
            {
                Status = StatusCodes.Status428PreconditionRequired,
                Title = "Precondition required",
                Detail = "An If-Match header with the resource's current ETag is required for this update."
            })
            { StatusCode = StatusCodes.Status428PreconditionRequired };
            return false;
        }

        try
        {
            rowVersion = Convert.FromBase64String(headerValue.Trim('"'));
            failure = null;
            return true;
        }
        catch (FormatException)
        {
            rowVersion = [];
            failure = new ObjectResult(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Bad request",
                Detail = "The If-Match header value is not a recognised ETag."
            })
            { StatusCode = StatusCodes.Status400BadRequest };
            return false;
        }
    }
}
