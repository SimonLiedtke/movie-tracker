using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using MovieTracker.Application;
using MovieTracker.Application.Movies;
using MovieTracker.Application.Watchlist;
using MovieTracker.Domain.Watchlist;
using MovieTracker.Infrastructure.Persistence.Queries;

namespace MovieTracker.Infrastructure.Persistence.Repositories;

internal sealed class WatchlistRepository(MovieTrackerDbContext db) : IWatchlistRepository
{
    private static readonly Expression<Func<WatchlistItem, WatchlistItemView>> ToView =
        item => new WatchlistItemView(
            new MovieRef(item.Movie.Id, item.Movie.Title, item.Movie.ReleaseDate, item.Movie.PosterUrl),
            item.AddedAt,
            item.Note);

    private IQueryable<WatchlistItem> OwnItems(Guid userId) => db.WatchlistItems.Where(item => item.UserId == userId);

    public async Task<PagedResult<WatchlistItemView>> ListAsync(
        Guid userId, int page, int pageSize, CancellationToken cancellationToken)
    {
        var items = OwnItems(userId).AsNoTracking();

        var totalCount = await items.CountAsync(cancellationToken);
        var views = await items
            .OrderByDescending(item => item.AddedAt)
            .ThenBy(item => item.MovieId)
            .Paginate(page, pageSize)
            .Select(ToView)
            .ToListAsync(cancellationToken);

        return new PagedResult<WatchlistItemView>(views, page, pageSize, totalCount);
    }

    public async Task<WatchlistItemView?> GetAsync(Guid userId, Guid movieId, CancellationToken cancellationToken) =>
        await OwnItems(userId)
            .AsNoTracking()
            .Where(item => item.MovieId == movieId)
            .Select(ToView)
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<WatchlistItem?> FindAsync(Guid userId, Guid movieId, CancellationToken cancellationToken) =>
        await db.WatchlistItems.FindAsync([userId, movieId], cancellationToken);

    public void Add(WatchlistItem item) => db.WatchlistItems.Add(item);

    public async Task<bool> RemoveAsync(Guid userId, Guid movieId, CancellationToken cancellationToken) =>
        await OwnItems(userId).Where(item => item.MovieId == movieId).ExecuteDeleteAsync(cancellationToken) > 0;
}
