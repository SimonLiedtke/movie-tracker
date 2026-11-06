using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using MovieTracker.Application.Tmdb;
using MovieTracker.Domain.Movies;

namespace MovieTracker.Infrastructure.Tmdb;

/// <summary>Client for the TMDB API v3, see https://developer.themoviedb.org/reference.</summary>
internal sealed class TmdbClient : ITmdbClient
{
    private const string PosterBaseUrl = "https://image.tmdb.org/t/p/w500";

    /// <summary>The language in which <see cref="FindMoviesAsync"/> compares titles: Letterboxd names films in English.</summary>
    private const string MatchingLanguage = "en-US";

    // Same limits as the database columns.
    private const int MaxTitleLength = 300;
    private const int MaxOverviewLength = 5000;

    private readonly HttpClient _http;
    private readonly TmdbOptions _options;

    public TmdbClient(HttpClient http, TmdbOptions options)
    {
        _http = http;
        _options = options;
        _http.BaseAddress = new Uri("https://api.themoviedb.org/3/");
        _http.Timeout = TimeSpan.FromSeconds(10);
        if (options.IsConfigured)
        {
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", options.AccessToken);
        }
    }

    public bool IsConfigured => _options.IsConfigured;

    public Task<IReadOnlyList<TmdbSearchResult>> SearchMoviesAsync(string query, CancellationToken cancellationToken) =>
        SearchAsync(query, year: null, _options.Language, cancellationToken);

    public Task<IReadOnlyList<TmdbSearchResult>> FindMoviesAsync(string title, int? year, CancellationToken cancellationToken) =>
        SearchAsync(title, year, MatchingLanguage, cancellationToken);

    private async Task<IReadOnlyList<TmdbSearchResult>> SearchAsync(
        string query, int? year, string language, CancellationToken cancellationToken)
    {
        var url = $"search/movie?query={Uri.EscapeDataString(query)}&include_adult=false&page=1&language={Uri.EscapeDataString(language)}";
        if (year is not null)
        {
            url += $"&year={year.Value.ToString(CultureInfo.InvariantCulture)}";
        }

        var page = await GetAsync<SearchPage>(url, cancellationToken)
            ?? throw new TmdbUnavailableException("TMDB returned an empty search response.");

        return page.Results
            .Where(movie => !string.IsNullOrWhiteSpace(movie.Title))
            .Select(movie => new TmdbSearchResult(
                movie.Id,
                Truncate(movie.Title!.Trim(), MaxTitleLength)!,
                Truncate(NullIfBlank(movie.OriginalTitle), MaxTitleLength),
                ParseDate(movie.ReleaseDate),
                PosterUrl(movie.PosterPath)))
            .ToList();
    }

    public async Task<TmdbMovie?> GetMovieAsync(int tmdbId, CancellationToken cancellationToken)
    {
        var url = $"movie/{tmdbId}?language={Uri.EscapeDataString(_options.Language)}";
        var movie = await GetAsync<MovieDetailsDto>(url, cancellationToken);
        if (movie is null)
        {
            return null;
        }

        var title = NullIfBlank(movie.Title) ?? NullIfBlank(movie.OriginalTitle)
            ?? throw new TmdbUnavailableException($"TMDB movie {tmdbId} has no title.");

        var details = new MovieDetails(
            Truncate(title.Trim(), MaxTitleLength)!,
            Truncate(NullIfBlank(movie.OriginalTitle), MaxTitleLength),
            ParseDate(movie.ReleaseDate),
            movie.Runtime is > 0 ? movie.Runtime : null,
            Truncate(NullIfBlank(movie.Overview), MaxOverviewLength),
            PosterUrl(movie.PosterPath));

        return new TmdbMovie(movie.Id, details, movie.Genres.Select(genre => genre.Id).ToList());
    }

    /// <summary>Returns null for 404.</summary>
    private async Task<T?> GetAsync<T>(string url, CancellationToken cancellationToken) where T : class
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException("TMDB is not configured.");
        }

        try
        {
            using var response = await _http.GetAsync(url, cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new TmdbUnavailableException($"TMDB answered with {(int)response.StatusCode}.");
            }

            return await response.Content.ReadFromJsonAsync<T>(cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            throw new TmdbUnavailableException("TMDB could not be reached.", exception);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TmdbUnavailableException("TMDB did not answer in time.", exception);
        }
        catch (System.Text.Json.JsonException exception)
        {
            throw new TmdbUnavailableException("TMDB returned an unexpected response.", exception);
        }
    }

    private static string? PosterUrl(string? posterPath) =>
        string.IsNullOrWhiteSpace(posterPath) ? null : PosterBaseUrl + posterPath;

    /// <summary>TMDB uses "" for unknown release dates.</summary>
    private static DateOnly? ParseDate(string? value) =>
        DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : null;

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static string? Truncate(string? value, int maxLength) =>
        value is { Length: > 0 } && value.Length > maxLength ? value[..maxLength] : value;

    private sealed record SearchPage(List<SearchResultDto> Results);

    private sealed record SearchResultDto(
        int Id,
        string? Title,
        [property: JsonPropertyName("original_title")] string? OriginalTitle,
        [property: JsonPropertyName("release_date")] string? ReleaseDate,
        [property: JsonPropertyName("poster_path")] string? PosterPath);

    private sealed record MovieDetailsDto(
        int Id,
        string? Title,
        [property: JsonPropertyName("original_title")] string? OriginalTitle,
        [property: JsonPropertyName("release_date")] string? ReleaseDate,
        int? Runtime,
        string? Overview,
        [property: JsonPropertyName("poster_path")] string? PosterPath,
        List<GenreDto> Genres);

    private sealed record GenreDto(int Id, string Name);
}
