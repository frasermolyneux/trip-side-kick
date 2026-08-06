using NodaTime;

namespace MX.TripSideKick.Web.Tests.Application;

/// <summary>A trivial fixed-instant <see cref="IClock"/> test double, avoiding a new NodaTime.Testing dependency.</summary>
internal sealed class FixedClock(Instant instant) : IClock
{
    public Instant GetCurrentInstant() => instant;
}
