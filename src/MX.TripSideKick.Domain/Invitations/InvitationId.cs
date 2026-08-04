namespace MX.TripSideKick.Domain.Invitations;

/// <summary>
/// Strongly-typed identifier for an <see cref="Invitation"/>.
/// </summary>
public readonly record struct InvitationId(Guid Value)
{
    public static InvitationId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}
