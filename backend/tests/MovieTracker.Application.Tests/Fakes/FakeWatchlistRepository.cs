using MovieTracker.Application;
using MovieTracker.Application.Movies;
using MovieTracker.Application.Watchlist;
using MovieTracker.Domain.Watchlist;

namespace MovieTracker.Application.Tests.Fakes;

/// <summary>An in-memory watchlist that follows the contract of <see cref="IWatchlistRepository"/>: every user has their own.</summary>
public sealed class FakeWatchlistRepository(FakeMovieRepository movies) : IWatchlistRepository
{
    public List<WatchlistItem> Items { get; } = [];

    public (Guid UserId, int Page, int PageSize)? LastList { get; private set; }

    public Task<PagedResult<WatchlistItemView>> ListAsync(Guid userId, int page, int pageSize, CancellationToken cancellationToken)
    {
        LastList = (userId, page, pageSize);

        var own = Items
            .Where(item => item.UserId == userId)
            .OrderByDescending(item => item.AddedAt)
            .ThenBy(item => item.MovieId)
            .ToList();
        var views = own.Skip((page - 1) * pageSize).Take(pageSize).Select(ToView).ToList();
        return Task.FromResult(new PagedResult<WatchlistItemView>(views, page, pageSize, own.Count));
    }

    public Task<WatchlistItemView?> GetAsync(Guid userId, Guid movieId, CancellationToken cancellationToken) =>
        Task.FromResult(Own(userId, movieId) is { } item ? ToView(item) : null);

    public Task<WatchlistItem?> FindAsync(Guid userId, Guid movieId, CancellationToken cancellationToken) =>
        Task.FromResult(Own(userId, movieId));

    public void Add(WatchlistItem item) => Items.Add(item);

    public void Remove(WatchlistItem item) => Items.Remove(item);

    public Task<bool> RemoveAsync(Guid userId, Guid movieId, CancellationToken cancellationToken) =>
        Task.FromResult(Own(userId, movieId) is { } item && Items.Remove(item));

    private WatchlistItem? Own(Guid userId, Guid movieId) =>
        Items.FirstOrDefault(item => item.UserId == userId && item.MovieId == movieId);

    private WatchlistItemView ToView(WatchlistItem item)
    {
        var movie = movies.Movies.Single(candidate => candidate.Id == item.MovieId);
        return new WatchlistItemView(
            new MovieRef(movie.Id, movie.Title, movie.ReleaseDate, movie.PosterUrl), item.AddedAt, item.Note);
    }
}
