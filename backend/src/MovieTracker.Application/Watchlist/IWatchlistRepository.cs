using MovieTracker.Domain.Watchlist;

namespace MovieTracker.Application.Watchlist;

/// <summary>Every user has their own watchlist: every method only sees the items of the given user.</summary>
public interface IWatchlistRepository
{
    /// <summary>The items matching the filter, most recently added first.</summary>
    Task<PagedResult<WatchlistItemView>> ListAsync(
        Guid userId, WatchlistFilter filter, int page, int pageSize, CancellationToken cancellationToken);

    Task<WatchlistItemView?> GetAsync(Guid userId, Guid movieId, CancellationToken cancellationToken);

    /// <summary>For changing the item: changes are saved with <see cref="IUnitOfWork"/>.</summary>
    Task<WatchlistItem?> FindAsync(Guid userId, Guid movieId, CancellationToken cancellationToken);

    /// <summary>Saved with <see cref="IUnitOfWork"/>.</summary>
    void Add(WatchlistItem item);

    /// <summary>For removing an item found with <see cref="FindAsync"/>: saved with <see cref="IUnitOfWork"/>.</summary>
    void Remove(WatchlistItem item);

    /// <summary>Deletes immediately. False if the movie was not on the watchlist.</summary>
    Task<bool> RemoveAsync(Guid userId, Guid movieId, CancellationToken cancellationToken);
}
