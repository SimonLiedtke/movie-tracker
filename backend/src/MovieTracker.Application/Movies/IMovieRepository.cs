using MovieTracker.Domain.Movies;

namespace MovieTracker.Application.Movies;

/// <summary>
/// Movies are shared (from TMDB) or private to their owner (added manually). Every method that takes a user ID treats
/// other users' manual movies as if they did not exist.
/// </summary>
public interface IMovieRepository
{
    /// <summary>"My movies": added manually, logged in the diary or on the watchlist.</summary>
    Task<PagedResult<MovieSummary>> ListInLibraryAsync(Guid userId, MovieListQuery query, CancellationToken cancellationToken);

    /// <summary>The user's manually added movies that match the text, best first.</summary>
    Task<IReadOnlyList<MovieSearchHit>> SearchOwnAsync(
        Guid userId, string query, int maxResults, CancellationToken cancellationToken);

    /// <summary>The IDs of those TMDB movies that are saved, by TMDB ID.</summary>
    Task<IReadOnlyDictionary<int, Guid>> FindIdsByTmdbIdsAsync(
        IReadOnlyCollection<int> tmdbIds, CancellationToken cancellationToken);

    Task<Guid?> FindIdByTmdbIdAsync(int tmdbId, CancellationToken cancellationToken);

    /// <summary>Null if the movie does not exist or is not visible to the user.</summary>
    Task<MovieDetail?> GetDetailAsync(Guid userId, Guid movieId, CancellationToken cancellationToken);

    Task<bool> ExistsAsync(Guid userId, Guid movieId, CancellationToken cancellationToken);

    /// <summary>
    /// For changing the movie (with its genres): changes are saved with <see cref="IUnitOfWork"/>.
    /// Null if the movie does not exist or is not visible to the user.
    /// </summary>
    Task<Movie?> FindAsync(Guid userId, Guid movieId, CancellationToken cancellationToken);

    /// <summary>Saved with <see cref="IUnitOfWork"/>.</summary>
    void Add(Movie movie);

    /// <summary>
    /// Saves a movie from TMDB immediately. If somebody else saved the same TMDB movie at the same moment, nothing is
    /// added and the ID of that movie is returned; otherwise the ID of <paramref name="movie"/>.
    /// </summary>
    Task<Guid> AddTmdbMovieAsync(Movie movie, CancellationToken cancellationToken);

    Task<bool> HasDiaryEntriesAsync(Guid movieId, CancellationToken cancellationToken);

    /// <summary>Deletes immediately, together with the movie's watchlist items.</summary>
    Task DeleteAsync(Guid movieId, CancellationToken cancellationToken);
}
