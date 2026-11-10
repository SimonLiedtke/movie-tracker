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
    // Projected into rows first, like the movie queries: the genres are mapped to the model in memory.
    private static readonly Expression<Func<WatchlistItem, WatchlistItemRow>> ToRow =
        item => new WatchlistItemRow(
            new MovieRef(item.Movie.Id, item.Movie.Title, item.Movie.ReleaseDate, item.Movie.PosterUrl),
            item.AddedAt,
            item.Note,
            item.Movie.Genres
                .OrderBy(genre => genre.Name)
                .Select(genre => new GenreRow(genre.Id, genre.Name))
                .ToList(),
            item.Movie.RuntimeMinutes);

    private IQueryable<WatchlistItem> OwnItems(Guid userId) => db.WatchlistItems.Where(item => item.UserId == userId);

    public async Task<WatchlistPage> ListAsync(
        Guid userId, WatchlistFilter filter, int page, int pageSize, CancellationToken cancellationToken)
    {
        var own = OwnItems(userId).AsNoTracking();
        var items = own.Matching(filter);

        var totalCount = await items.CountAsync(cancellationToken);
        var unfilteredCount = filter.IsEmpty ? totalCount : await own.CountAsync(cancellationToken);
        var views = await items
            .OrderByDescending(item => item.AddedAt)
            .ThenBy(item => item.MovieId)
            .Paginate(page, pageSize)
            .Select(ToRow)
            .ToListAsync(cancellationToken);

        return new WatchlistPage(views.Select(row => row.ToModel()).ToList(), page, pageSize, totalCount, unfilteredCount);
    }

    public async Task<WatchlistItemView?> GetAsync(Guid userId, Guid movieId, CancellationToken cancellationToken) =>
        (await OwnItems(userId)
            .AsNoTracking()
            .Where(item => item.MovieId == movieId)
            .Select(ToRow)
            .SingleOrDefaultAsync(cancellationToken))?.ToModel();

    public async Task<WatchlistItem?> FindAsync(Guid userId, Guid movieId, CancellationToken cancellationToken) =>
        await db.WatchlistItems.FindAsync([userId, movieId], cancellationToken);

    public void Add(WatchlistItem item) => db.WatchlistItems.Add(item);

    public void Remove(WatchlistItem item) => db.WatchlistItems.Remove(item);
}

internal sealed record WatchlistItemRow(
    MovieRef Movie, DateTimeOffset AddedAt, string? Note, List<GenreRow> Genres, int? RuntimeMinutes)
{
    public WatchlistItemView ToModel() =>
        new(Movie, AddedAt, Note, Genres.Select(genre => genre.ToModel()).ToList(), RuntimeMinutes);
}
