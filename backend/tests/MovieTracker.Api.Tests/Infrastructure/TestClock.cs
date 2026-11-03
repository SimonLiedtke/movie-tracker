namespace MovieTracker.Api.Tests.Infrastructure;

/// <summary>
/// A clock that tests control. Unlike FakeTimeProvider, it can be set back, so it can be reset between tests.
/// </summary>
public sealed class TestClock(DateTimeOffset utcNow) : TimeProvider
{
    private DateTimeOffset _utcNow = utcNow;

    public override DateTimeOffset GetUtcNow() => _utcNow;

    public void Set(DateTimeOffset utcNow) => _utcNow = utcNow;

    public void Advance(TimeSpan delta) => _utcNow += delta;
}
