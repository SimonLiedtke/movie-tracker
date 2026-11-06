using MovieTracker.Application;

namespace MovieTracker.Infrastructure.Persistence;

/// <summary>One database context per request is one unit of work: saving it saves what all repositories changed.</summary>
internal sealed class EfUnitOfWork(MovieTrackerDbContext db) : IUnitOfWork
{
    public async Task SaveChangesAsync(CancellationToken cancellationToken) =>
        await db.SaveChangesAsync(cancellationToken);
}
