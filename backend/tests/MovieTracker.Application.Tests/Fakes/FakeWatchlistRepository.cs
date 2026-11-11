using MovieTracker.Application;
using MovieTracker.Application.Movies;
using MovieTracker.Application.Watchlist;
using MovieTracker.Domain.Watchlist;

namespace MovieTracker.Application.Tests.Fakes;

/// <summary>An in-memory watchlist that follows the contract of <see cref="IWatchlistRepository"/>: every user has their own.</summary>
public sealed class FakeWatchlistRepository(FakeMovieRepository movies) : IWatchlistRepository
{
    public List<WatchlistItem> Items { get; } = [];

    public (Guid UserId, WatchlistFilter Filter, int Page, int PageSize)? LastList { get; private set; }

    public Task<WatchlistPage> ListAsync(
        Guid userId, WatchlistFilter filter, int page, int pageSize, CancellationToken cancellationToken)
    {
        LastList = (userId, filter, page, pageSize);

        var all = Items.Where(item => item.UserId == userId).ToList();
        var own = all
            .Where(item => Matches(item, filter))
            .OrderByDescending(item => item.AddedAt)
            .ThenBy(item => item.MovieId)
            .ToList();
        var views = own.Skip((page - 1) * pageSize).Take(pageSize).Select(ToView).ToList();
        return Task.FromResult(new WatchlistPage(views, page, pageSize, own.Count, all.Count));
    }

    public Task<WatchlistItemView?> GetAsync(Guid userId, Guid movieId, CancellationToken cancellationToken) =>
        Task.FromResult(Own(userId, movieId) is { } item ? ToView(item) : null);

    public Task<WatchlistItem?> FindAsync(Guid userId, Guid movieId, CancellationToken cancellationToken) =>
        Task.FromResult(Own(userId, movieId));

    public void Add(WatchlistItem item) => Items.Add(item);

    public void Remove(WatchlistItem item) => Items.Remove(item);

    private bool Matches(WatchlistItem item, WatchlistFilter filter)
    {
        var movie = movies.Movies.Single(candidate => candidate.Id == item.MovieId);
        var ids = filter.GenreIds.Distinct().ToList();
        var movieGenreIds = movie.Genres.Select(genre => genre.Id).ToList();
        var genresMatch = ids.Count == 0 || (filter.GenreMatch == GenreMatch.All
            ? ids.All(movieGenreIds.Contains)
            : ids.Any(movieGenreIds.Contains));
        var releasedFromMatches = filter.ReleasedFrom is null || movie.ReleaseDate >= filter.ReleasedFrom;
        var releasedToMatches = filter.ReleasedTo is null || movie.ReleaseDate <= filter.ReleasedTo;
        var addedDay = DateOnly.FromDateTime(item.AddedAt.UtcDateTime);
        var addedFromMatches = filter.AddedFrom is null || addedDay >= filter.AddedFrom;
        var addedToMatches = filter.AddedTo is null || addedDay <= filter.AddedTo;
        var runtimeFromMatches = filter.RuntimeFrom is null || movie.RuntimeMinutes >= filter.RuntimeFrom;
        var runtimeToMatches = filter.RuntimeTo is null || movie.RuntimeMinutes <= filter.RuntimeTo;
        var searchMatches = string.IsNullOrWhiteSpace(filter.Search)
            || movie.Title.Contains(filter.Search.Trim(), StringComparison.OrdinalIgnoreCase)
            || (movie.OriginalTitle?.Contains(filter.Search.Trim(), StringComparison.OrdinalIgnoreCase) ?? false);
        var tagMatches = filter.Tag is null || item.Tags.Contains(filter.Tag);
        return searchMatches && tagMatches && genresMatch && releasedFromMatches && releasedToMatches && addedFromMatches && addedToMatches
            && runtimeFromMatches && runtimeToMatches;
    }

    private WatchlistItem? Own(Guid userId, Guid movieId) =>
        Items.FirstOrDefault(item => item.UserId == userId && item.MovieId == movieId);

    private WatchlistItemView ToView(WatchlistItem item)
    {
        var movie = movies.Movies.Single(candidate => candidate.Id == item.MovieId);
        return new WatchlistItemView(
            new MovieRef(movie.Id, movie.Title, movie.ReleaseDate, movie.PosterUrl),
            item.AddedAt,
            item.Note,
            item.Tags,
            movie.Genres.OrderBy(genre => genre.Name).ToList(),
            movie.RuntimeMinutes);
    }
}
