using MovieTracker.Application.Activity;
using MovieTracker.Application.Movies;
using MovieTracker.Application.Watchlist;
using MovieTracker.Domain.Activity;
using MovieTracker.Domain.Diary;
using MovieTracker.Domain.Tags;

namespace MovieTracker.Application.Diary;

public sealed class DiaryService(
    IDiaryRepository diary,
    IWatchlistRepository watchlist,
    IMovieRepository movies,
    IActivityLogRepository activityLog,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public Task<PagedResult<DiaryEntryView>> ListAsync(
        Guid userId, DiaryFilter filter, int page, int pageSize, CancellationToken cancellationToken) =>
        diary.ListAsync(userId, filter, page, pageSize, cancellationToken);

    /// <summary>The user's entries of a movie, newest first. Null if the movie does not exist or is not visible to the user.</summary>
    public async Task<IReadOnlyList<DiaryEntryView>?> ListForMovieAsync(
        Guid userId, Guid movieId, CancellationToken cancellationToken)
    {
        if (!await movies.ExistsAsync(userId, movieId, cancellationToken))
        {
            return null;
        }

        return await diary.ListForMovieAsync(userId, movieId, cancellationToken);
    }

    public Task<DiaryEntryView?> GetAsync(Guid userId, Guid entryId, CancellationToken cancellationToken) =>
        diary.GetAsync(userId, entryId, cancellationToken);

    /// <summary>
    /// Logs the viewing. The movie has been watched, so it is taken off the user's watchlist, in the same save: either
    /// the viewing is logged and the movie is off the watchlist, or nothing changes. The activity log says so.
    /// The tags of the watchlist item move to the entry along with it: the entry gets them in addition to its own tags.
    /// </summary>
    public async Task<SaveDiaryEntryResult> CreateAsync(
        Guid userId, DiaryEntryDetails details, CancellationToken cancellationToken)
    {
        if (IsInFuture(details.WatchedOn))
        {
            return new SaveDiaryEntryResult(SaveDiaryEntryStatus.FutureDate);
        }

        if (await movies.GetTitleAsync(userId, details.MovieId, cancellationToken) is not { } title)
        {
            return new SaveDiaryEntryResult(SaveDiaryEntryStatus.UnknownMovie);
        }

        var now = timeProvider.GetUtcNow();
        var watchlistItem = await watchlist.FindAsync(userId, details.MovieId, cancellationToken);
        if (watchlistItem is not null)
        {
            details = details with { Tags = TagList.Merge(details.Tags, watchlistItem.Tags) };
            watchlist.Remove(watchlistItem);
        }

        var entry = new DiaryEntry(userId, details, now);
        diary.Add(entry);

        activityLog.Add(ActivityLogEntry.ForMovie(
            userId,
            ActivityType.DiaryEntryAdded,
            now,
            details.MovieId,
            title,
            ActivityDetails.Viewing(details, removedFromWatchlist: watchlistItem is not null)));
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new SaveDiaryEntryResult(SaveDiaryEntryStatus.Saved, await diary.GetAsync(userId, entry.Id, cancellationToken));
    }

    public async Task<SaveDiaryEntryResult> UpdateAsync(
        Guid userId, Guid entryId, DiaryEntryDetails details, CancellationToken cancellationToken)
    {
        var entry = await diary.FindAsync(userId, entryId, cancellationToken);
        if (entry is null)
        {
            return new SaveDiaryEntryResult(SaveDiaryEntryStatus.NotFound);
        }

        if (IsInFuture(details.WatchedOn))
        {
            return new SaveDiaryEntryResult(SaveDiaryEntryStatus.FutureDate);
        }

        if (await movies.GetTitleAsync(userId, details.MovieId, cancellationToken) is not { } title)
        {
            return new SaveDiaryEntryResult(SaveDiaryEntryStatus.UnknownMovie);
        }

        var before = ToDetails(entry);
        entry.Update(details);
        // Saving the same data again changes nothing, so there is nothing to log.
        if (ActivityDetails.DiaryEntryChanges(before, details) is { } changes)
        {
            activityLog.Add(ActivityLogEntry.ForMovie(
                userId, ActivityType.DiaryEntryUpdated, timeProvider.GetUtcNow(), details.MovieId, title, changes));
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new SaveDiaryEntryResult(SaveDiaryEntryStatus.Saved, await diary.GetAsync(userId, entryId, cancellationToken));
    }

    /// <summary>False if there is no such entry.</summary>
    public async Task<bool> DeleteAsync(Guid userId, Guid entryId, CancellationToken cancellationToken)
    {
        var entry = await diary.FindAsync(userId, entryId, cancellationToken);
        if (entry is null)
        {
            return false;
        }

        // The movie is always visible to the owner of an entry; the fallback only keeps the deletion from failing.
        var title = await movies.GetTitleAsync(userId, entry.MovieId, cancellationToken) ?? "Unknown movie";
        diary.Remove(entry);
        activityLog.Add(ActivityLogEntry.ForMovie(
            userId,
            ActivityType.DiaryEntryDeleted,
            timeProvider.GetUtcNow(),
            entry.MovieId,
            title,
            $"Watched on {entry.WatchedOn:yyyy-MM-dd}"));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static DiaryEntryDetails ToDetails(DiaryEntry entry) =>
        new(entry.MovieId, entry.WatchedOn, entry.Rating, entry.Review, entry.IsRewatch, entry.Liked, entry.Tags);

    private bool IsInFuture(DateOnly watchedOn)
    {
        // One day of tolerance: the client's local date can already be ahead of the UTC date.
        var latestAllowed = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime).AddDays(1);
        return watchedOn > latestAllowed;
    }
}
