using MovieTracker.Domain.Activity;

namespace MovieTracker.Application.Activity;

/// <summary>
/// Shows the user what they did. The entries are written by the use cases that change something, in the same save as
/// the change, so the log neither misses a change nor shows one that did not happen.
/// </summary>
public sealed class ActivityLogService(IActivityLogRepository log)
{
    /// <summary>The user's actions that modified the database, most recent first.</summary>
    public Task<PagedResult<ActivityLogEntry>> ListAsync(
        Guid userId, int page, int pageSize, CancellationToken cancellationToken) =>
        log.ListAsync(userId, page, pageSize, cancellationToken);
}
