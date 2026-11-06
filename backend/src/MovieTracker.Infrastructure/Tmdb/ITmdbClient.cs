using MovieTracker.Domain.Movies;

namespace MovieTracker.Infrastructure.Tmdb;

/// <summary>Read access to The Movie Database (TMDB).</summary>
public interface ITmdbClient
{
    /// <summary>False if no access token is configured. The other methods must not be called then.</summary>
    bool IsConfigured { get; }

    /// <summary>The first page of TMDB's search results, most relevant first.</summary>
    /// <exception cref="TmdbUnavailableException">TMDB could not be reached or answered with an error.</exception>
    Task<IReadOnlyList<TmdbSearchResult>> SearchMoviesAsync(string query, CancellationToken cancellationToken);

    /// <summary>
    /// Searches by title, optionally only movies released in the given year. Unlike <see cref="SearchMoviesAsync"/>, the titles
    /// are always TMDB's English ones, whatever language is configured: this is for finding the movie that another source
    /// (e.g. Letterboxd) means by comparing titles, not for showing results.
    /// </summary>
    /// <exception cref="TmdbUnavailableException">TMDB could not be reached or answered with an error.</exception>
    Task<IReadOnlyList<TmdbSearchResult>> FindMoviesAsync(string title, int? year, CancellationToken cancellationToken);

    /// <summary>Returns null if TMDB does not know the ID.</summary>
    /// <exception cref="TmdbUnavailableException">TMDB could not be reached or answered with an error.</exception>
    Task<TmdbMovie?> GetMovieAsync(int tmdbId, CancellationToken cancellationToken);
}

public sealed record TmdbSearchResult(int TmdbId, string Title, string? OriginalTitle, DateOnly? ReleaseDate, string? PosterUrl);

/// <summary>A movie's details. The genre IDs are TMDB's, which the seeded genres use as well.</summary>
public sealed record TmdbMovie(int TmdbId, MovieDetails Details, IReadOnlyList<int> GenreIds);

public sealed class TmdbUnavailableException(string message, Exception? innerException = null)
    : Exception(message, innerException);
