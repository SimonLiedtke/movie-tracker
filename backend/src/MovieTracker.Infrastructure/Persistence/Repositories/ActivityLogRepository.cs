using Microsoft.EntityFrameworkCore;
using MovieTracker.Application;
using MovieTracker.Application.Activity;
using MovieTracker.Domain.Activity;
using MovieTracker.Infrastructure.Persistence.Queries;

namespace MovieTracker.Infrastructure.Persistence.Repositories;

internal sealed class ActivityLogRepository(MovieTrackerDbContext db) : IActivityLogRepository
{
    public async Task<PagedResult<ActivityLogEntry>> ListAsync(
        Guid userId, int page, int pageSize, CancellationToken cancellationToken)
    {
        var own = db.ActivityLogEntries.AsNoTracking().Where(entry => entry.UserId == userId);

        var totalCount = await own.CountAsync(cancellationToken);
        var entries = await own
            .OrderByDescending(entry => entry.OccurredAt)
            .ThenByDescending(entry => entry.Id)
            .Paginate(page, pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<ActivityLogEntry>(entries, page, pageSize, totalCount);
    }

    public void Add(ActivityLogEntry entry) => db.ActivityLogEntries.Add(entry);
}
