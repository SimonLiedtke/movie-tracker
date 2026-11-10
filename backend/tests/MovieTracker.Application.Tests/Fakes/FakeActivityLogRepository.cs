using MovieTracker.Application;
using MovieTracker.Application.Activity;
using MovieTracker.Domain.Activity;

namespace MovieTracker.Application.Tests.Fakes;

/// <summary>An in-memory activity log that follows the contract of <see cref="IActivityLogRepository"/>: every user has their own.</summary>
public sealed class FakeActivityLogRepository : IActivityLogRepository
{
    public List<ActivityLogEntry> Entries { get; } = [];

    public Task<PagedResult<ActivityLogEntry>> ListAsync(
        Guid userId, int page, int pageSize, CancellationToken cancellationToken)
    {
        var own = Entries
            .Where(entry => entry.UserId == userId)
            .OrderByDescending(entry => entry.OccurredAt)
            .ThenByDescending(entry => entry.Id)
            .ToList();
        return Task.FromResult(new PagedResult<ActivityLogEntry>(
            own.Skip((page - 1) * pageSize).Take(pageSize).ToList(), page, pageSize, own.Count));
    }

    public void Add(ActivityLogEntry entry) => Entries.Add(entry);
}
