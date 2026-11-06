namespace MovieTracker.Application;

/// <summary>
/// Saves everything that was added to or changed in the repositories during the use case, in one transaction.
/// (Repository methods that say so execute immediately and do not need it.)
/// </summary>
public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
