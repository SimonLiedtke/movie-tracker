namespace MovieTracker.Application.Tests.Fakes;

/// <summary>
/// Counts the saves. The fake repositories apply changes to their entities at once, so a test that wants to know whether a
/// use case saved what it changed (and did not save when it refused the request) looks at <see cref="SaveCount"/>.
/// </summary>
public sealed class FakeUnitOfWork : IUnitOfWork
{
    public int SaveCount { get; private set; }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        SaveCount++;
        return Task.CompletedTask;
    }
}
