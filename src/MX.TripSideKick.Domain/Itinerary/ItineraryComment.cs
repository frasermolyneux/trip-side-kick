using MX.TripSideKick.Domain.Trips;

using NodaTime;

namespace MX.TripSideKick.Domain.Itinerary;

/// <summary>
/// Append-only comment on an <see cref="ItineraryItem"/>. Any trip member (including Viewers) may
/// add a comment; comments are the one mutation Viewers are allowed to perform on trip content in
/// this slice. No edit or delete APIs in this slice - hence no <c>RowVersion</c>.
/// </summary>
public sealed class ItineraryComment
{
    private ItineraryComment()
    {
    }

    public ItineraryCommentId Id { get; private init; }

    public TripId TripId { get; private init; }

    public ItineraryItemId ItineraryItemId { get; private init; }

    /// <summary>Stable Entra <c>oid</c> of the comment author. Never email.</summary>
    public string AuthorSubjectId { get; private init; } = string.Empty;

    public string Body { get; private init; } = string.Empty;

    public Instant CreatedAt { get; private init; }

    /// <summary>Maximum allowed length of <see cref="Body"/>.</summary>
    public const int MaxBodyLength = 2000;

    public static ItineraryComment Create(
        TripId tripId, ItineraryItemId itineraryItemId, string authorSubjectId, string body, Instant createdAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(authorSubjectId);
        ArgumentException.ThrowIfNullOrWhiteSpace(body);
        if (body.Length > MaxBodyLength)
        {
            throw new ArgumentException(
                $"Comment body cannot be longer than {MaxBodyLength} characters.", nameof(body));
        }

        return new ItineraryComment
        {
            Id = ItineraryCommentId.New(),
            TripId = tripId,
            ItineraryItemId = itineraryItemId,
            AuthorSubjectId = authorSubjectId,
            Body = body.Trim(),
            CreatedAt = createdAt
        };
    }
}
