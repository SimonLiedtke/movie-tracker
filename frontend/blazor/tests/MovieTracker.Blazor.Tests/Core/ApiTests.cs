using System.Net;
using MovieTracker.Blazor.Tests.Support;

namespace MovieTracker.Blazor.Tests.Core;

public class ApiTests
{
    private readonly FakeBackend backend = new();
    private readonly Api api;
    private readonly ApiClient client;

    public ApiTests()
    {
        client = new ApiClient(new HttpClient(backend) { BaseAddress = new Uri("http://localhost/") });
        api = new Api(client);
    }

    [Fact]
    public async Task Movies_are_listed_with_the_query()
    {
        backend.Get("/api/movies", Samples.Page([Samples.Summary(1)], pageSize: 24));

        var page = await api.Movies.ListAsync(new MovieQuery("a b", 2, 1999, MovieSort.TitleDescending, 3, 24));

        Assert.Single(page.Items);
        Assert.Equal("?search=a%20b&genreId=2&year=1999&sort=-title&page=3&pageSize=24", backend.Last(HttpMethod.Get, "/api/movies").Query);
    }

    [Fact]
    public async Task Movies_are_listed_without_a_query()
    {
        backend.Get("/api/movies", Samples.Page<MovieSummary>([]));

        await api.Movies.ListAsync();

        Assert.Equal("", backend.Last(HttpMethod.Get, "/api/movies").Query);
    }

    [Fact]
    public async Task The_movie_search_sends_the_text()
    {
        backend.Get("/api/movies/search", new MovieSearchResponse([], true));

        var response = await api.Movies.SearchAsync("blade runner");

        Assert.True(response.TmdbAvailable);
        Assert.Equal("?query=blade%20runner", backend.Last(HttpMethod.Get, "/api/movies/search").Query);
    }

    [Fact]
    public async Task A_movie_is_added_with_a_json_body()
    {
        backend.Json(HttpMethod.Post, "/api/movies", Samples.Movie(1, tmdbId: null), HttpStatusCode.Created);

        await api.Movies.CreateAsync(new MovieRequest("Title", null, new DateOnly(2020, 2, 3), 90, null, "https://x.test/p.jpg", [1, 2]));

        var sent = backend.Last(HttpMethod.Post, "/api/movies");
        Assert.Equal("application/json", sent.ContentType);
        Assert.Equal(
            """{"title":"Title","originalTitle":null,"releaseDate":"2020-02-03","runtimeMinutes":90,"overview":null,"posterUrl":"https://x.test/p.jpg","genreIds":[1,2]}""",
            sent.Body);
    }

    [Fact]
    public async Task A_poster_is_uploaded_as_a_form_with_the_file_field()
    {
        var movie = Samples.Movie(1, tmdbId: null, posterUrl: $"/api/movies/{Samples.Id(1)}/poster");
        backend.Json(HttpMethod.Put, $"/api/movies/{Samples.Id(1)}/poster", movie);

        var updated = await api.Movies.UploadPosterAsync(Samples.Id(1), "poster.png", "image/png", new MemoryStream([1, 2, 3]));

        Assert.Equal(movie.PosterUrl, updated.PosterUrl);
        var sent = backend.Last(HttpMethod.Put, $"/api/movies/{Samples.Id(1)}/poster");
        Assert.Equal("multipart/form-data", sent.ContentType);
        Assert.Contains("name=file", sent.Body);
        Assert.Contains("filename=poster.png", sent.Body);
        Assert.Contains("Content-Type: image/png", sent.Body);
    }

    [Fact]
    public async Task The_genres_are_asked_for_once()
    {
        backend.Get("/api/genres", Samples.Genres);
        var first = await api.Genres.ListAsync();
        var second = await api.Genres.ListAsync();

        Assert.Equal(first, second);
        Assert.Single(backend.Sent(HttpMethod.Get, "/api/genres"));
    }

    [Fact]
    public async Task The_genres_are_asked_for_again_after_an_error()
    {
        backend.Problem(HttpMethod.Get, "/api/genres", HttpStatusCode.InternalServerError, "Broken");
        await Assert.ThrowsAsync<ApiException>(() => api.Genres.ListAsync());

        backend.Get("/api/genres", Samples.Genres);
        var genres = await api.Genres.ListAsync();

        Assert.Equal(Samples.Genres, genres);
    }

    [Fact]
    public async Task The_diary_is_listed_with_the_filters()
    {
        backend.Get("/api/diary-entries", Samples.Page<DiaryEntry>([]));

        await api.Diary.ListAsync(new DiaryQuery("2026-01-01", "2026-12-31", null, "cinema", 3, 8, 2, 25));

        Assert.Equal(
            "?from=2026-01-01&to=2026-12-31&tag=cinema&ratingFrom=3&ratingTo=8&page=2&pageSize=25",
            backend.Last(HttpMethod.Get, "/api/diary-entries").Query);
    }

    [Fact]
    public async Task The_watchlist_sends_the_match_mode_only_with_genres()
    {
        backend.Get("/api/watchlist", Samples.WatchlistPage([]));

        await api.Watchlist.ListAsync(new WatchlistQuery(GenreMatch: GenreMatch.All, Page: 1));
        await api.Watchlist.ListAsync(new WatchlistQuery(GenreIds: [2, 3], GenreMatch: GenreMatch.All, Page: 1));
        await api.Watchlist.ListAsync(new WatchlistQuery(GenreIds: [2]));

        var queries = backend.Sent(HttpMethod.Get, "/api/watchlist").Select(r => r.Query).ToList();
        Assert.Equal("?page=1", queries[0]);
        Assert.Equal("?page=1&genreId=2&genreId=3&genreMatch=all", queries[1]);
        Assert.Equal("?genreId=2&genreMatch=any", queries[2]);
    }

    [Fact]
    public async Task The_watchlist_sends_all_filters_and_trims_text()
    {
        backend.Get("/api/watchlist", Samples.WatchlistPage([]));

        await api.Watchlist.ListAsync(new WatchlistQuery(
            ReleasedFrom: "1979-05-25", ReleasedTo: "1989-11-03", AddedFrom: "2026-01-01", AddedTo: "2026-02-01",
            RuntimeFrom: 90, RuntimeTo: 120, Search: "  alien ", Tag: " weekend ", Page: 2, PageSize: 20));

        Assert.Equal(
            "?page=2&pageSize=20&releasedFrom=1979-05-25&releasedTo=1989-11-03&addedFrom=2026-01-01&addedTo=2026-02-01"
            + "&runtimeFrom=90&runtimeTo=120&search=alien&tag=weekend",
            backend.Last(HttpMethod.Get, "/api/watchlist").Query);
    }

    [Fact]
    public async Task A_note_and_tags_are_put_on_the_watchlist()
    {
        backend.Json(HttpMethod.Put, $"/api/watchlist/{Samples.Id(5)}", Samples.Item(5));

        await api.Watchlist.PutAsync(Samples.Id(5), "Why", ["a", "b"]);
        await api.Watchlist.PutAsync(Samples.Id(5), null);

        var bodies = backend.Sent(HttpMethod.Put, $"/api/watchlist/{Samples.Id(5)}").Select(r => r.Body).ToList();
        Assert.Equal("""{"note":"Why","tags":["a","b"]}""", bodies[0]);
        Assert.Equal("""{"note":null,"tags":[]}""", bodies[1]);
    }

    [Fact]
    public async Task The_activity_log_is_read_with_enum_names_and_times()
    {
        backend.On(HttpMethod.Get, "/api/activity-log", _ => FakeBackend.JsonResponse(new
        {
            items = new[]
            {
                new { id = Samples.Id(1), type = "watchlistTagsChanged", occurredAt = "2026-11-07T21:30:00+00:00", movieId = (Guid?)null, movieTitle = "Gone", detail = "a, b" },
            },
            page = 1, pageSize = 50, totalCount = 1,
        }));

        var page = await api.ActivityLog.ListAsync(new PageQuery(1, 50));

        var entry = Assert.Single(page.Items);
        Assert.Equal(ActivityType.WatchlistTagsChanged, entry.Type);
        Assert.Equal(new DateTimeOffset(2026, 11, 7, 21, 30, 0, TimeSpan.Zero), entry.OccurredAt);
        Assert.Null(entry.MovieId);
        Assert.Equal("?page=1&pageSize=50", backend.Last(HttpMethod.Get, "/api/activity-log").Query);
    }

    [Fact]
    public async Task An_import_job_is_read()
    {
        var id = Guid.NewGuid();
        backend.On(HttpMethod.Get, $"/api/imports/letterboxd/{id}", _ => FakeBackend.JsonResponse(new
        {
            id, status = "completed", filmsTotal = 3, filmsProcessed = 3, error = (string?)null,
            result = new
            {
                diaryEntriesImported = 2, diaryEntriesSkipped = 1, watchlistItemsImported = 0, watchlistItemsSkipped = 0,
                manualMovies = new[] { new { name = "Obscure", year = (int?)1971 } },
                failedFilms = Array.Empty<object>(), warnings = new[] { "Row 4" },
            },
        }));

        var job = await api.LetterboxdImport.GetAsync(id);

        Assert.Equal(ImportStatus.Completed, job.Status);
        Assert.Equal(new ImportFilm("Obscure", 1971), job.Result!.ManualMovies.Single());
    }

    [Fact]
    public async Task An_error_becomes_the_problems_detail_and_validation_messages()
    {
        backend.Problem(HttpMethod.Post, "/api/movies", HttpStatusCode.BadRequest, "One or more validation errors occurred.",
            new() { ["title"] = ["Too long."], ["runtimeMinutes"] = ["Out of range.", "Too small."] });

        var exception = await Assert.ThrowsAsync<ApiException>(() =>
            api.Movies.CreateAsync(new MovieRequest("x", null, null, null, null, null, [])));

        Assert.Equal(400, exception.Error.Status);
        Assert.Equal("One or more validation errors occurred.", exception.Error.Message);
        Assert.Equal(["Too long.", "Out of range.", "Too small."], exception.Error.Details);
    }

    [Fact]
    public async Task An_error_without_problem_details_has_the_status_as_message()
    {
        backend.On(HttpMethod.Get, "/api/genres", _ => new HttpResponseMessage(HttpStatusCode.BadGateway)
        {
            Content = new StringContent("<html>Bad gateway</html>", System.Text.Encoding.UTF8, "text/html"),
        });

        var exception = await Assert.ThrowsAsync<ApiException>(() => api.Genres.ListAsync());

        Assert.Equal(502, exception.Error.Status);
        Assert.Equal("Request failed (HTTP 502).", exception.Error.Message);
        Assert.Empty(exception.Error.Details);
    }

    [Fact]
    public void A_request_that_got_no_answer_says_so()
    {
        var error = ApiError.From(new HttpRequestException("Failed to fetch"));

        Assert.Equal(0, error.Status);
        Assert.Equal("The API cannot be reached. Is the backend running on http://localhost:5080?", error.Message);
    }

    [Fact]
    public void Other_errors_are_unexpected()
    {
        Assert.Equal("An unexpected error occurred.", ApiError.From(new InvalidOperationException()).Message);
    }

    [Fact]
    public async Task A_missing_thing_is_not_found()
    {
        var exception = await Assert.ThrowsAsync<ApiException>(() => api.Movies.GetAsync(Samples.Id(9)));

        Assert.True(exception.Error.IsNotFound);
    }
}
