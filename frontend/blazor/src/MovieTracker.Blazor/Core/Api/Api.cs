namespace MovieTracker.Blazor.Core.Api;

/// <summary>Typed clients for the API's endpoints.</summary>
public sealed class Api(ApiClient client)
{
    public GenresApi Genres { get; } = new(client);
    public MoviesApi Movies { get; } = new(client);
    public DiaryApi Diary { get; } = new(client);
    public WatchlistApi Watchlist { get; } = new(client);
    public LetterboxdImportApi LetterboxdImport { get; } = new(client);
    public ActivityLogApi ActivityLog { get; } = new(client);
}

public sealed class GenresApi(ApiClient client)
{
    // Genres are seeded and do not change at runtime, so one request per session is enough.
    private Task<Genre[]>? genres;

    public async Task<IReadOnlyList<Genre>> ListAsync()
    {
        var pending = genres ??= client.GetAsync("/api/genres", ApiJson.Default.GenreArray);
        try
        {
            return await pending;
        }
        catch
        {
            genres = null; // Try again the next time.
            throw;
        }
    }
}

public sealed class MoviesApi(ApiClient client)
{
    /// <summary>The user's movies: added manually, logged or on the watchlist.</summary>
    public Task<PagedResponse<MovieSummary>> ListAsync(MovieQuery? query = null)
    {
        query ??= new MovieQuery();
        return client.GetAsync(
            "/api/movies" + QueryString.Build(
                ("search", query.Search),
                ("genreId", query.GenreId),
                ("year", query.Year),
                ("sort", query.Sort),
                ("page", query.Page),
                ("pageSize", query.PageSize)),
            ApiJson.Default.PagedResponseMovieSummary);
    }

    /// <summary>Finds movies to log or watchlist: the user's manual movies and TMDB's results.</summary>
    public Task<MovieSearchResponse> SearchAsync(string query) =>
        client.GetAsync("/api/movies/search" + QueryString.Build(("query", query)), ApiJson.Default.MovieSearchResponse);

    /// <summary>Makes a TMDB movie available for diary and watchlist. Returns the existing movie if it was imported before.</summary>
    public Task<Movie> ImportFromTmdbAsync(int tmdbId) =>
        client.PostAsync($"/api/movies/tmdb/{tmdbId}", ApiJson.Default.Movie);

    public Task<Movie> GetAsync(Guid id) => client.GetAsync($"/api/movies/{id}", ApiJson.Default.Movie);

    /// <summary>Adds a movie manually. Only the signed-in user sees it.</summary>
    public Task<Movie> CreateAsync(MovieRequest request) =>
        client.PostJsonAsync("/api/movies", request, ApiJson.Default.MovieRequest, ApiJson.Default.Movie);

    public Task<Movie> UpdateAsync(Guid id, MovieRequest request) =>
        client.PutJsonAsync($"/api/movies/{id}", request, ApiJson.Default.MovieRequest, ApiJson.Default.Movie);

    public Task DeleteAsync(Guid id) => client.SendAsync(HttpMethod.Delete, $"/api/movies/{id}");

    /// <summary>
    /// Sets the poster of a manually added movie from an image (the formats and the size limit are those of
    /// <c>PUT /api/movies/{movieId}/poster</c>), replacing its previous poster. Returns the movie, whose
    /// <c>PosterUrl</c> is then the image's URL.
    /// </summary>
    public Task<Movie> UploadPosterAsync(Guid id, string fileName, string contentType, Stream image) =>
        client.PutAsync($"/api/movies/{id}/poster", FileForm(fileName, contentType, image), ApiJson.Default.Movie);

    public Task<DiaryEntry[]> DiaryEntriesAsync(Guid id) =>
        client.GetAsync($"/api/movies/{id}/diary-entries", ApiJson.Default.DiaryEntryArray);

    /// <summary>A multipart form with the file in the field "file".</summary>
    internal static MultipartFormDataContent FileForm(string fileName, string contentType, Stream content)
    {
        var file = new StreamContent(content);
        if (!string.IsNullOrEmpty(contentType))
        {
            file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        }

        return new MultipartFormDataContent { { file, "file", fileName } };
    }
}

public sealed class DiaryApi(ApiClient client)
{
    public Task<PagedResponse<DiaryEntry>> ListAsync(DiaryQuery? query = null)
    {
        query ??= new DiaryQuery();
        return client.GetAsync(
            "/api/diary-entries" + QueryString.Build(
                ("from", query.From),
                ("to", query.To),
                ("movieId", query.MovieId),
                ("tag", query.Tag),
                ("ratingFrom", query.RatingFrom),
                ("ratingTo", query.RatingTo),
                ("page", query.Page),
                ("pageSize", query.PageSize)),
            ApiJson.Default.PagedResponseDiaryEntry);
    }

    public Task<DiaryEntry> GetAsync(Guid id) => client.GetAsync($"/api/diary-entries/{id}", ApiJson.Default.DiaryEntry);

    public Task<DiaryEntry> CreateAsync(DiaryEntryRequest request) =>
        client.PostJsonAsync("/api/diary-entries", request, ApiJson.Default.DiaryEntryRequest, ApiJson.Default.DiaryEntry);

    public Task<DiaryEntry> UpdateAsync(Guid id, DiaryEntryRequest request) =>
        client.PutJsonAsync($"/api/diary-entries/{id}", request, ApiJson.Default.DiaryEntryRequest, ApiJson.Default.DiaryEntry);

    public Task DeleteAsync(Guid id) => client.SendAsync(HttpMethod.Delete, $"/api/diary-entries/{id}");
}

public sealed class WatchlistApi(ApiClient client)
{
    public Task<WatchlistPage> ListAsync(WatchlistQuery? query = null)
    {
        query ??= new WatchlistQuery();
        var filtered = query.GenreIds is { Count: > 0 };
        return client.GetAsync(
            "/api/watchlist" + QueryString.Build(
                ("page", query.Page),
                ("pageSize", query.PageSize),
                ("genreId", query.GenreIds),
                // The match mode means nothing without genres.
                ("genreMatch", filtered ? (query.GenreMatch ?? GenreMatch.Any).ToString().ToLowerInvariant() : null),
                ("releasedFrom", query.ReleasedFrom),
                ("releasedTo", query.ReleasedTo),
                ("addedFrom", query.AddedFrom),
                ("addedTo", query.AddedTo),
                ("runtimeFrom", query.RuntimeFrom),
                ("runtimeTo", query.RuntimeTo),
                ("search", query.Search?.Trim()),
                ("tag", query.Tag?.Trim())),
            ApiJson.Default.WatchlistPage);
    }

    /// <summary>The different tags on the watchlist, alphabetically: what the tag filter can offer.</summary>
    public Task<string[]> TagsAsync() => client.GetAsync("/api/watchlist/tags", ApiJson.Default.StringArray);

    /// <summary>Adds the movie to the watchlist, or replaces its note and tags if it is already on it.</summary>
    public Task<WatchlistItem> PutAsync(Guid movieId, string? note, IReadOnlyList<string>? tags = null) =>
        client.PutJsonAsync(
            $"/api/watchlist/{movieId}",
            new WatchlistItemRequest(note, tags ?? []),
            ApiJson.Default.WatchlistItemRequest,
            ApiJson.Default.WatchlistItem);

    public Task RemoveAsync(Guid movieId) => client.SendAsync(HttpMethod.Delete, $"/api/watchlist/{movieId}");
}

public sealed class LetterboxdImportApi(ApiClient client)
{
    /// <summary>Uploads the ZIP archive of a Letterboxd export. The import runs on the server in the background.</summary>
    public Task<ImportJob> StartAsync(string fileName, string contentType, Stream archive) =>
        client.PostAsync("/api/imports/letterboxd", MoviesApi.FileForm(fileName, contentType, archive), ApiJson.Default.ImportJob);

    /// <summary>The progress of an import, or its result. 404 if the server does not know the import (any more).</summary>
    public Task<ImportJob> GetAsync(Guid id) => client.GetAsync($"/api/imports/letterboxd/{id}", ApiJson.Default.ImportJob);
}

public sealed class ActivityLogApi(ApiClient client)
{
    /// <summary>What the user did that modified the database, most recent first.</summary>
    public Task<PagedResponse<ActivityLogEntry>> ListAsync(PageQuery? query = null)
    {
        query ??= new PageQuery();
        return client.GetAsync(
            "/api/activity-log" + QueryString.Build(("page", query.Page), ("pageSize", query.PageSize)),
            ApiJson.Default.PagedResponseActivityLogEntry);
    }
}
