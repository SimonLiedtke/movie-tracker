using MovieTracker.Application.Movies;
using MovieTracker.Domain.Watchlist;

namespace MovieTracker.Application.Watchlist;

public sealed class WatchlistService(
    IWatchlistRepository watchlist, IMovieRepository movies, IUnitOfWork unitOfWork, TimeProvider timeProvider)
{
    public Task<PagedResult<WatchlistItemView>> ListAsync(
        Guid userId, int page, int pageSize, CancellationToken cancellationToken) =>
        watchlist.ListAsync(userId, page, pageSize, cancellationToken);

    /// <summary>Puts the movie on the watchlist, or replaces the note if it is on it already (idempotent).</summary>
    public async Task<PutWatchlistItemResult> PutAsync(
        Guid userId, Guid movieId, string? note, CancellationToken cancellationToken)
    {
        var item = await watchlist.FindAsync(userId, movieId, cancellationToken);
        if (item is not null)
        {
            item.UpdateNote(note);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return new PutWatchlistItemResult(
                PutWatchlistItemStatus.Updated, await watchlist.GetAsync(userId, movieId, cancellationToken));
        }

        if (!await movies.ExistsAsync(userId, movieId, cancellationToken))
        {
            return new PutWatchlistItemResult(PutWatchlistItemStatus.NotFound);
        }

        watchlist.Add(new WatchlistItem(userId, movieId, note, timeProvider.GetUtcNow()));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new PutWatchlistItemResult(
            PutWatchlistItemStatus.Added, await watchlist.GetAsync(userId, movieId, cancellationToken));
    }

    /// <summary>False if the movie was not on the watchlist.</summary>
    public Task<bool> RemoveAsync(Guid userId, Guid movieId, CancellationToken cancellationToken) =>
        watchlist.RemoveAsync(userId, movieId, cancellationToken);
}
