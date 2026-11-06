namespace MovieTracker.Application.Tests.Fakes;

/// <summary>A clock that tests control.</summary>
public sealed class TestClock(DateTimeOffset utcNow) : TimeProvider
{
    private DateTimeOffset _utcNow = utcNow;

    public override DateTimeOffset GetUtcNow() => _utcNow;

    public void Advance(TimeSpan delta) => _utcNow += delta;
}
