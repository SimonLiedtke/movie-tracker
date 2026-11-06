using MovieTracker.Domain.Movies;
using MovieTracker.Application.Tmdb;

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
    private int _getMovieCalls;
    private int _findCalls;

    public bool IsConfigured { get; set; }

    /// <summary>Simulates TMDB being down.</summary>
    public bool IsUnavailable { get; set; }

    public int GetMovieCalls => _getMovieCalls;

    /// <summary>How often <see cref="FindMoviesAsync"/> was called (the import runs several lookups at once).</summary>
    public int FindCalls => _findCalls;

    /// <summary>Titles for which <see cref="FindMoviesAsync"/> fails, as if TMDB had a problem with just these.</summary>
    public HashSet<string> FailingTitles { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The title that <see cref="FindMoviesAsync"/> sees for a movie, if it differs from the title in <see cref="GetMovieAsync"/>:
    /// the former is always English, the latter in the configured language.
    /// </summary>
    public Dictionary<int, string> EnglishTitles { get; } = [];

    /// <summary>While set, <see cref="FindMoviesAsync"/> waits for it to complete. Lets a test look at a running import.</summary>
    public TaskCompletionSource? FindGate { get; set; }

    public void Reset()
    {
        IsConfigured = true;
        IsUnavailable = false;
        _getMovieCalls = 0;
        _findCalls = 0;
        FailingTitles.Clear();
        EnglishTitles.Clear();
        FindGate = null;
        _movies.Clear();
        _movies[Alien.TmdbId] = Alien;
        _movies[Aliens.TmdbId] = Aliens;
    }

    public void Add(TmdbMovie movie) => _movies[movie.TmdbId] = movie;

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

    /// <summary>Like TMDB: a title search that finds more than the exact title, optionally limited to a release year.</summary>
    public async Task<IReadOnlyList<TmdbSearchResult>> FindMoviesAsync(string title, int? year, CancellationToken cancellationToken)
    {
        ThrowIfUnusable();
        Interlocked.Increment(ref _findCalls);

        if (FindGate is { } gate)
        {
            await gate.Task.WaitAsync(cancellationToken);
        }

        if (FailingTitles.Contains(title))
        {
            throw new TmdbUnavailableException($"TMDB failed for '{title}' (fake).");
        }

        return _movies.Values
            .Select(movie => (Movie: movie, Title: EnglishTitles.GetValueOrDefault(movie.TmdbId, movie.Details.Title)))
            .Where(candidate => candidate.Title.Contains(title, StringComparison.OrdinalIgnoreCase))
            .Where(candidate => year is null || candidate.Movie.Details.ReleaseDate?.Year == year)
            .OrderBy(candidate => candidate.Movie.Details.ReleaseDate)
            .Select(candidate => new TmdbSearchResult(
                candidate.Movie.TmdbId,
                candidate.Title,
                candidate.Movie.Details.OriginalTitle,
                candidate.Movie.Details.ReleaseDate,
                candidate.Movie.Details.PosterUrl))
            .ToList();
    }

    public Task<TmdbMovie?> GetMovieAsync(int tmdbId, CancellationToken cancellationToken)
    {
        ThrowIfUnusable();
        Interlocked.Increment(ref _getMovieCalls);
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
