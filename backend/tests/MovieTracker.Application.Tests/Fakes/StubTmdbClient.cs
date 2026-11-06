using MovieTracker.Application.Tmdb;

namespace MovieTracker.Application.Tests.Fakes;

/// <summary>Stands in for TMDB. Like the real client, it must not be used while it is not configured.</summary>
public sealed class StubTmdbClient : ITmdbClient
{
    public bool IsConfigured { get; set; } = true;

    /// <summary>Simulates TMDB being down.</summary>
    public bool IsUnavailable { get; set; }

    public List<TmdbSearchResult> SearchResults { get; } = [];

    public Dictionary<int, TmdbMovie> Movies { get; } = [];

    /// <summary>The queries that were searched for.</summary>
    public List<string> Searches { get; } = [];

    public int GetMovieCalls { get; private set; }

    public Task<IReadOnlyList<TmdbSearchResult>> SearchMoviesAsync(string query, CancellationToken cancellationToken)
    {
        ThrowIfUnusable();
        Searches.Add(query);
        return Task.FromResult<IReadOnlyList<TmdbSearchResult>>(SearchResults.ToList());
    }

    public Task<IReadOnlyList<TmdbSearchResult>> FindMoviesAsync(string title, int? year, CancellationToken cancellationToken) =>
        throw new NotSupportedException("Only the Letterboxd import matches films by title.");

    public Task<TmdbMovie?> GetMovieAsync(int tmdbId, CancellationToken cancellationToken)
    {
        ThrowIfUnusable();
        GetMovieCalls++;
        return Task.FromResult(Movies.GetValueOrDefault(tmdbId));
    }

    private void ThrowIfUnusable()
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException("TMDB is not configured, so it must not be called.");
        }

        if (IsUnavailable)
        {
            throw new TmdbUnavailableException("TMDB is down (stub).");
        }
    }
}
