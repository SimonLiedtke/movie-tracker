using System.Net;
using System.Net.Http.Json;
using MovieTracker.Api.Contracts;
using MovieTracker.Api.Tests.Infrastructure;

namespace MovieTracker.Api.Tests;

public sealed class ActivityLogTests(ApiFactory factory) : ApiTestBase(factory)
{
    private Task<PagedResponse<ActivityLogEntryResponse>> ListAsync(string query = "") =>
        GetAsync<PagedResponse<ActivityLogEntryResponse>>($"/api/activity-log{query}");

    [Fact]
    public async Task The_log_is_empty_at_first()
    {
        var result = await ListAsync();

        Assert.Empty(result.Items);
        Assert.Equal((1, 20, 0), (result.Page, result.PageSize, result.TotalCount));
    }

    [Fact]
    public async Task Logging_a_viewing_adds_a_line_with_the_time()
    {
        var movie = await CreateMovieAsync("Alien");
        Factory.Clock.Advance(TimeSpan.FromHours(2));

        await CreateDiaryEntryAsync(movie.Id, "2026-10-31", rating: 9);

        var entry = Assert.Single((await ListAsync()).Items.Where(item => item.Type == ActivityLogType.DiaryEntryAdded));
        Assert.Equal((movie.Id, "Alien", ApiFactory.StartTime.AddHours(2)), (entry.MovieId, entry.MovieTitle, entry.OccurredAt));
        Assert.Equal("Watched on 2026-10-31 · 4.5 stars", entry.Detail);
    }

    [Fact]
    public async Task Types_are_written_in_camel_case()
    {
        var movie = await CreateMovieAsync();
        await Client.PutAsync($"/api/watchlist/{movie.Id}", content: null, Ct);

        var json = await Client.GetStringAsync("/api/activity-log", Ct);

        Assert.Contains("\"type\":\"watchlistItemAdded\"", json);
        Assert.Contains("\"type\":\"movieAdded\"", json);
    }

    [Fact]
    public async Task Every_kind_of_change_is_logged_most_recent_first()
    {
        var movie = await CreateMovieAsync("Alien");
        var tick = TimeSpan.FromMinutes(1);
        Factory.Clock.Advance(tick);
        await Client.PutAsJsonAsync($"/api/watchlist/{movie.Id}", new { note = "tonight" }, Ct);
        Factory.Clock.Advance(tick);
        await Client.PutAsJsonAsync($"/api/watchlist/{movie.Id}", new { note = "tomorrow" }, Ct);
        Factory.Clock.Advance(tick);
        await Client.DeleteAsync($"/api/watchlist/{movie.Id}", Ct);
        Factory.Clock.Advance(tick);
        var diaryEntry = await CreateDiaryEntryAsync(movie.Id, rating: 6);
        Factory.Clock.Advance(tick);
        await Client.PutAsJsonAsync(
            $"/api/diary-entries/{diaryEntry.Id}", new { movieId = movie.Id, watchedOn = "2026-10-31", rating = 10 }, Ct);
        Factory.Clock.Advance(tick);
        await Client.DeleteAsync($"/api/diary-entries/{diaryEntry.Id}", Ct);
        Factory.Clock.Advance(tick);
        await Client.PutAsJsonAsync(
            $"/api/movies/{movie.Id}",
            new { title = "Aliens", releaseDate = "1986-07-18", runtimeMinutes = 137, genreIds = new[] { Horror } },
            Ct);
        Factory.Clock.Advance(tick);
        await Client.DeleteAsync($"/api/movies/{movie.Id}", Ct);

        var result = await ListAsync();

        Assert.Equal(
            [
                (ActivityLogType.MovieDeleted, "Aliens", null),
                (ActivityLogType.MovieUpdated, "Aliens", null),
                (ActivityLogType.DiaryEntryDeleted, "Alien", "Watched on 2026-10-31"),
                (ActivityLogType.DiaryEntryUpdated, "Alien", "Rating 3 stars → 5 stars"),
                (ActivityLogType.DiaryEntryAdded, "Alien", "Watched on 2026-10-31 · 3 stars"),
                (ActivityLogType.WatchlistItemRemoved, "Alien", null),
                (ActivityLogType.WatchlistNoteChanged, "Alien", "Note: tomorrow"),
                (ActivityLogType.WatchlistItemAdded, "Alien", "Note: tonight"),
                (ActivityLogType.MovieAdded, "Alien", null),
            ],
            result.Items.Select(item => (item.Type, item.MovieTitle, item.Detail)));
        Assert.Equal(9, result.TotalCount);
    }

    [Fact]
    public async Task A_deleted_movie_stays_in_the_log_by_its_title_but_without_a_link()
    {
        var movie = await CreateMovieAsync("Home Video");
        Factory.Clock.Advance(TimeSpan.FromMinutes(1));

        await Client.DeleteAsync($"/api/movies/{movie.Id}", Ct);

        var items = (await ListAsync()).Items;
        // The earlier line about the movie lost its reference, the title stays.
        Assert.All(items, item => Assert.Null(item.MovieId));
        Assert.All(items, item => Assert.Equal("Home Video", item.MovieTitle));
        Assert.Equal([ActivityLogType.MovieDeleted, ActivityLogType.MovieAdded], items.Select(item => item.Type));
    }

    [Fact]
    public async Task Logging_a_viewing_of_a_watchlisted_movie_says_so_in_one_line()
    {
        var movie = await CreateMovieAsync();
        await Client.PutAsync($"/api/watchlist/{movie.Id}", content: null, Ct);

        await CreateDiaryEntryAsync(movie.Id, rating: null);

        var entry = (await ListAsync()).Items.First(item => item.Type == ActivityLogType.DiaryEntryAdded);
        Assert.Equal("Watched on 2026-10-31 · removed from the watchlist", entry.Detail);
    }

    [Fact]
    public async Task Importing_a_movie_from_TMDB_is_logged_once()
    {
        await ImportTmdbMovieAsync();
        await ImportTmdbMovieAsync();

        var entry = Assert.Single((await ListAsync()).Items);
        Assert.Equal(ActivityLogType.MovieImported, entry.Type);
        Assert.Equal(FakeTmdbClient.Alien.Details.Title, entry.MovieTitle);
    }

    [Fact]
    public async Task Changes_that_change_nothing_and_failed_requests_are_not_logged()
    {
        var movie = await CreateMovieAsync();
        await Client.PutAsJsonAsync($"/api/watchlist/{movie.Id}", new { note = "same" }, Ct);
        var before = (await ListAsync()).TotalCount;

        await Client.PutAsJsonAsync($"/api/watchlist/{movie.Id}", new { note = "same" }, Ct);
        await Client.DeleteAsync($"/api/watchlist/{Guid.NewGuid()}", Ct);
        await Client.DeleteAsync($"/api/diary-entries/{Guid.NewGuid()}", Ct);
        await Client.PostAsJsonAsync("/api/diary-entries", new { movieId = movie.Id, watchedOn = "2999-01-01" }, Ct);

        Assert.Equal(before, (await ListAsync()).TotalCount);
    }

    [Fact]
    public async Task Users_only_see_their_own_actions()
    {
        var movie = await CreateMovieAsync("Alien");
        await Client.PutAsync($"/api/watchlist/{movie.Id}", content: null, Ct);
        await CreateMovieAsync("Heat", client: Bob);

        var bobsLog = await ReadAsync<PagedResponse<ActivityLogEntryResponse>>(
            await Bob.GetAsync("/api/activity-log", Ct), HttpStatusCode.OK);
        var alicesLog = await ListAsync();

        Assert.Equal(["Heat"], bobsLog.Items.Select(item => item.MovieTitle));
        Assert.Equal(["Alien", "Alien"], alicesLog.Items.Select(item => item.MovieTitle));
    }

    [Fact]
    public async Task The_log_is_paged()
    {
        for (var i = 1; i <= 5; i++)
        {
            await CreateMovieAsync($"Movie {i}");
            Factory.Clock.Advance(TimeSpan.FromMinutes(1));
        }

        var first = await ListAsync("?pageSize=2");
        var last = await ListAsync("?pageSize=2&page=3");
        var beyond = await ListAsync("?pageSize=2&page=4");

        Assert.Equal(["Movie 5", "Movie 4"], first.Items.Select(item => item.MovieTitle));
        Assert.Equal((1, 2, 5), (first.Page, first.PageSize, first.TotalCount));
        Assert.Equal(["Movie 1"], last.Items.Select(item => item.MovieTitle));
        Assert.Empty(beyond.Items);
        Assert.Equal(5, beyond.TotalCount);
    }

    [Theory]
    [InlineData("?page=0")]
    [InlineData("?pageSize=0")]
    [InlineData("?pageSize=101")]
    public async Task Invalid_paging_is_rejected(string query)
    {
        var response = await Client.GetAsync($"/api/activity-log{query}", Ct);

        await ReadValidationProblemAsync(response);
    }

    [Fact]
    public async Task The_log_requires_signing_in()
    {
        var response = await Anonymous.GetAsync("/api/activity-log", Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
