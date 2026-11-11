using MovieTracker.Application.Activity;
using MovieTracker.Application.Movies;
using MovieTracker.Domain.Activity;
using MovieTracker.Domain.Watchlist;

namespace MovieTracker.Application.Watchlist;

public sealed class WatchlistService(
    IWatchlistRepository watchlist,
    IMovieRepository movies,
    IActivityLogRepository activityLog,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public Task<WatchlistPage> ListAsync(
        Guid userId, WatchlistFilter filter, int page, int pageSize, CancellationToken cancellationToken) =>
        watchlist.ListAsync(userId, filter, page, pageSize, cancellationToken);

    /// <summary>Puts the movie on the watchlist, or replaces the note and the tags if it is on it already (idempotent).</summary>
    public async Task<PutWatchlistItemResult> PutAsync(
        Guid userId, Guid movieId, string? note, IReadOnlyList<string>? tags, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var item = await watchlist.FindAsync(userId, movieId, cancellationToken);
        if (item is not null)
        {
            // Putting the same note or tags again changes nothing, so there is nothing to log.
            var tagChange = ActivityDetails.WatchlistTagChange(item.Tags, tags);
            if (item.Note != note || tagChange is not null)
            {
                var title = await movies.GetTitleAsync(userId, movieId, cancellationToken) ?? "Unknown movie";
                if (item.Note != note)
                {
                    activityLog.Add(ActivityLogEntry.ForMovie(
                        userId, ActivityType.WatchlistNoteChanged, now, movieId, title, ActivityDetails.NoteChange(note)));
                }

                if (tagChange is not null)
                {
                    activityLog.Add(ActivityLogEntry.ForMovie(
                        userId, ActivityType.WatchlistTagsChanged, now, movieId, title, tagChange));
                }
            }

            item.UpdateNote(note);
            item.UpdateTags(tags);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return new PutWatchlistItemResult(
                PutWatchlistItemStatus.Updated, await watchlist.GetAsync(userId, movieId, cancellationToken));
        }

        if (await movies.GetTitleAsync(userId, movieId, cancellationToken) is not { } movieTitle)
        {
            return new PutWatchlistItemResult(PutWatchlistItemStatus.NotFound);
        }

        watchlist.Add(new WatchlistItem(userId, movieId, note, now, tags));
        activityLog.Add(ActivityLogEntry.ForMovie(
            userId, ActivityType.WatchlistItemAdded, now, movieId, movieTitle, ActivityDetails.WatchlistItem(note, tags)));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new PutWatchlistItemResult(
            PutWatchlistItemStatus.Added, await watchlist.GetAsync(userId, movieId, cancellationToken));
    }

    /// <summary>False if the movie was not on the watchlist.</summary>
    public async Task<bool> RemoveAsync(Guid userId, Guid movieId, CancellationToken cancellationToken)
    {
        var item = await watchlist.FindAsync(userId, movieId, cancellationToken);
        if (item is null)
        {
            return false;
        }

        var title = await movies.GetTitleAsync(userId, movieId, cancellationToken) ?? "Unknown movie";
        watchlist.Remove(item);
        activityLog.Add(ActivityLogEntry.ForMovie(
            userId, ActivityType.WatchlistItemRemoved, timeProvider.GetUtcNow(), movieId, title));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}
