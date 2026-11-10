using MovieTracker.Domain.Activity;

namespace MovieTracker.Application.Activity;

/// <summary>Every user has their own log: every method only sees the entries of the given user.</summary>
public interface IActivityLogRepository
{
    /// <summary>Most recent first.</summary>
    Task<PagedResult<ActivityLogEntry>> ListAsync(Guid userId, int page, int pageSize, CancellationToken cancellationToken);

    /// <summary>Saved with <see cref="IUnitOfWork"/>, together with the change that is logged.</summary>
    void Add(ActivityLogEntry entry);
}
