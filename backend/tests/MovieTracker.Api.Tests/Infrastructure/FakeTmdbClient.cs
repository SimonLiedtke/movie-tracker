using MovieTracker.Domain.Movies;
using MovieTracker.Infrastructure.Tmdb;

namespace MovieTracker.Api.Tests.Infrastructure;

/// <summary>Stands in for TMDB, so tests neither need an access token nor the network.</summary>
public sealed class FakeTmdbClient : ITmdbClient
{
    public static readonly TmdbMovie Alien = new(
        348,
        new MovieDetails("Alien", "Alien", new DateOnly(1979, 5, 25), 117, "In space no one can hear you scream.",
            "https://image.tmdb.org/t/p/w500/alien.jpg"),
        [27, 878]);

    public static readonly TmdbMovie Aliens = new(
        679,
        new MovieDetails("Aliens", "Aliens", new DateOnly(1986, 7, 18), 137, null, null),
        [28, 878, 999_999]); // 999999: a genre that is not seeded

    private readonly Dictionary<int, TmdbMovie> _movies = [];

    public bool IsConfigured { get; set; }

    /// <summary>Simulates TMDB being down.</summary>
    public bool IsUnavailable { get; set; }

    public int GetMovieCalls { get; private set; }

    public void Reset()
    {
        IsConfigured = true;
        IsUnavailable = false;
        GetMovieCalls = 0;
        _movies.Clear();
        _movies[Alien.TmdbId] = Alien;
        _movies[Aliens.TmdbId] = Aliens;
    }

    public Task<IReadOnlyList<TmdbSearchResult>> SearchMoviesAsync(string query, CancellationToken cancellationToken)
    {
        ThrowIfUnusable();
        IReadOnlyList<TmdbSearchResult> results = _movies.Values
            .Where(movie => movie.Details.Title.Contains(query, StringComparison.OrdinalIgnoreCase))
            .OrderBy(movie => movie.Details.ReleaseDate)
            .Select(movie => new TmdbSearchResult(
                movie.TmdbId, movie.Details.Title, movie.Details.OriginalTitle, movie.Details.ReleaseDate, movie.Details.PosterUrl))
            .ToList();
        return Task.FromResult(results);
    }

    public Task<TmdbMovie?> GetMovieAsync(int tmdbId, CancellationToken cancellationToken)
    {
        ThrowIfUnusable();
        GetMovieCalls++;
        return Task.FromResult(_movies.GetValueOrDefault(tmdbId));
    }

    private void ThrowIfUnusable()
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException("TMDB is not configured.");
        }

        if (IsUnavailable)
        {
            throw new TmdbUnavailableException("TMDB is down (fake).");
        }
    }
}
