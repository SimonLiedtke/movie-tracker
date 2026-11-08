using MovieTracker.Application.Movies;
using MovieTracker.Application.Watchlist;
using MovieTracker.Domain.Diary;

namespace MovieTracker.Application.Diary;

public sealed class DiaryService(
    IDiaryRepository diary,
    IWatchlistRepository watchlist,
    IMovieRepository movies,
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
    /// the viewing is logged and the movie is off the watchlist, or nothing changes.
    /// </summary>
    public async Task<SaveDiaryEntryResult> CreateAsync(
        Guid userId, DiaryEntryDetails details, CancellationToken cancellationToken)
    {
        if (IsInFuture(details.WatchedOn))
        {
            return new SaveDiaryEntryResult(SaveDiaryEntryStatus.FutureDate);
        }

        if (!await movies.ExistsAsync(userId, details.MovieId, cancellationToken))
        {
            return new SaveDiaryEntryResult(SaveDiaryEntryStatus.UnknownMovie);
        }

        var entry = new DiaryEntry(userId, details, timeProvider.GetUtcNow());
        diary.Add(entry);
        if (await watchlist.FindAsync(userId, details.MovieId, cancellationToken) is { } watchlistItem)
        {
            watchlist.Remove(watchlistItem);
        }

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

        if (!await movies.ExistsAsync(userId, details.MovieId, cancellationToken))
        {
            return new SaveDiaryEntryResult(SaveDiaryEntryStatus.UnknownMovie);
        }

        entry.Update(details);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new SaveDiaryEntryResult(SaveDiaryEntryStatus.Saved, await diary.GetAsync(userId, entryId, cancellationToken));
    }

    /// <summary>False if there is no such entry.</summary>
    public Task<bool> DeleteAsync(Guid userId, Guid entryId, CancellationToken cancellationToken) =>
        diary.DeleteAsync(userId, entryId, cancellationToken);

    private bool IsInFuture(DateOnly watchedOn)
    {
        // One day of tolerance: the client's local date can already be ahead of the UTC date.
        var latestAllowed = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime).AddDays(1);
        return watchedOn > latestAllowed;
    }
}
