using MovieTracker.Application;
using MovieTracker.Application.Movies;
using MovieTracker.Domain.Movies;

namespace MovieTracker.Application.Tests.Fakes;

/// <summary>
/// An in-memory movie repository that follows the contract of <see cref="IMovieRepository"/>: TMDB movies are visible to
/// everybody, manual movies only to their owner.
/// </summary>
public sealed class FakeMovieRepository : IMovieRepository
{
    public List<Movie> Movies { get; } = [];

    /// <summary>The movies that have diary entries.</summary>
    public HashSet<Guid> MoviesInDiary { get; } = [];

    /// <summary>Runs just before a TMDB movie is saved: lets a test save the same movie "at the same moment".</summary>
    public Action? BeforeAddTmdbMovie { get; set; }

    public PagedResult<MovieSummary> ListResult { get; set; } = new([], 1, 20, 0);

    public (Guid UserId, MovieListQuery Query)? LastList { get; private set; }

    public Task<PagedResult<MovieSummary>> ListInLibraryAsync(
        Guid userId, MovieListQuery query, CancellationToken cancellationToken)
    {
        LastList = (userId, query);
        return Task.FromResult(ListResult);
    }

    public Task<IReadOnlyList<MovieSearchHit>> SearchOwnAsync(
        Guid userId, string query, int maxResults, CancellationToken cancellationToken)
    {
        var hits = Movies
            .Where(movie => movie.OwnerId == userId)
            .Where(movie => movie.Title.Contains(query.Trim(), StringComparison.OrdinalIgnoreCase))
            .OrderBy(movie => movie.Title)
            .Take(maxResults)
            .Select(movie => new MovieSearchHit(movie.Id, null, movie.Title, movie.OriginalTitle, movie.ReleaseDate, movie.PosterUrl))
            .ToList();
        return Task.FromResult<IReadOnlyList<MovieSearchHit>>(hits);
    }

    public Task<IReadOnlyDictionary<int, Guid>> FindIdsByTmdbIdsAsync(
        IReadOnlyCollection<int> tmdbIds, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<int, Guid>>(Movies
            .Where(movie => movie.TmdbId is { } tmdbId && tmdbIds.Contains(tmdbId))
            .ToDictionary(movie => movie.TmdbId!.Value, movie => movie.Id));

    public Task<Guid?> FindIdByTmdbIdAsync(int tmdbId, CancellationToken cancellationToken) =>
        Task.FromResult(Movies.FirstOrDefault(movie => movie.TmdbId == tmdbId)?.Id);

    public Task<MovieDetail?> GetDetailAsync(Guid userId, Guid movieId, CancellationToken cancellationToken) =>
        Task.FromResult(Visible(userId, movieId) is { } movie ? ToDetail(movie) : null);

    public Task<bool> ExistsAsync(Guid userId, Guid movieId, CancellationToken cancellationToken) =>
        Task.FromResult(Visible(userId, movieId) is not null);

    public Task<Movie?> FindAsync(Guid userId, Guid movieId, CancellationToken cancellationToken) =>
        Task.FromResult(Visible(userId, movieId));

    public void Add(Movie movie) => Movies.Add(movie);

    public Task<Guid> AddTmdbMovieAsync(Movie movie, CancellationToken cancellationToken)
    {
        BeforeAddTmdbMovie?.Invoke();

        // The database has a unique index on the TMDB ID: the second one is not added.
        if (Movies.FirstOrDefault(existing => existing.TmdbId == movie.TmdbId) is { } concurrent)
        {
            return Task.FromResult(concurrent.Id);
        }

        Movies.Add(movie);
        return Task.FromResult(movie.Id);
    }

    public Task<bool> HasDiaryEntriesAsync(Guid movieId, CancellationToken cancellationToken) =>
        Task.FromResult(MoviesInDiary.Contains(movieId));

    public Task DeleteAsync(Guid movieId, CancellationToken cancellationToken)
    {
        Movies.RemoveAll(movie => movie.Id == movieId);
        return Task.CompletedTask;
    }

    private Movie? Visible(Guid userId, Guid movieId) =>
        Movies.FirstOrDefault(movie => movie.Id == movieId && movie.IsVisibleTo(userId));

    private static MovieDetail ToDetail(Movie movie) => new(
        movie.Id,
        movie.Title,
        movie.OriginalTitle,
        movie.ReleaseDate,
        movie.RuntimeMinutes,
        movie.Overview,
        movie.PosterUrl,
        movie.TmdbId,
        movie.Genres.OrderBy(genre => genre.Name).ToList(),
        LatestRating: null,
        WatchCount: 0,
        OnWatchlist: false,
        LastWatchedOn: null,
        movie.CreatedAt);
}
