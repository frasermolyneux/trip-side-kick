namespace MX.TripSideKick.Domain.Itinerary;

/// <summary>Categorises entries in a trip's collaborative activity feed.</summary>
public enum TripActivityFeedEventType
{
    ItemCreated = 0,
    ItemUpdated = 1,
    ItemScheduled = 2,
    ItemUnscheduled = 3,
    ItemDeleted = 4,
    CommentAdded = 5
}
