using System.Net;
using System.Net.Http.Json;
using MovieTracker.Api.Contracts;
using MovieTracker.Api.Tests.Infrastructure;

namespace MovieTracker.Api.Tests;

public sealed class DiaryEntriesTests(ApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task Create_returns_the_new_entry_with_its_location()
    {
        var movie = await CreateMovieAsync();

        var response = await Client.PostAsJsonAsync("/api/diary-entries", new
        {
            movieId = movie.Id,
            watchedOn = "2026-10-31",
            rating = 9,
            review = "Still terrifying.",
            isRewatch = true,
            liked = true,
        }, Ct);

        var entry = await ReadAsync<DiaryEntryResponse>(response, HttpStatusCode.Created);
        Assert.Equal($"/api/diary-entries/{entry.Id}", response.Headers.Location?.AbsolutePath);
        Assert.Equal(new MovieRefResponse(movie.Id, "Alien", new DateOnly(1979, 5, 25), null), entry.Movie);
        Assert.Equal(new DateOnly(2026, 10, 31), entry.WatchedOn);
        Assert.Equal(9, entry.Rating);
        Assert.Equal("Still terrifying.", entry.Review);
        Assert.True(entry.IsRewatch);
        Assert.True(entry.Liked);
        Assert.Equal(ApiFactory.StartTime, entry.CreatedAt);
    }

    [Fact]
    public async Task Create_takes_the_movie_off_the_watchlist()
    {
        var movie = await CreateMovieAsync();
        var other = await CreateMovieAsync(title: "Aliens");
        await Client.PutAsync($"/api/watchlist/{movie.Id}", content: null, Ct);
        await Client.PutAsync($"/api/watchlist/{other.Id}", content: null, Ct);

        await CreateDiaryEntryAsync(movie.Id);

        var watchlist = await GetAsync<PagedResponse<WatchlistItemResponse>>("/api/watchlist");
        Assert.Equal(other.Id, Assert.Single(watchlist.Items).Movie.Id);
        Assert.False((await GetAsync<MovieResponse>($"/api/movies/{movie.Id}")).OnWatchlist);
    }

    [Fact]
    public async Task Create_does_not_take_the_movie_off_the_watchlist_of_another_user()
    {
        var movie = await ImportTmdbMovieAsync();
        await Bob.PutAsync($"/api/watchlist/{movie.Id}", content: null, Ct);

        await CreateDiaryEntryAsync(movie.Id);

        var response = await Bob.GetAsync("/api/watchlist", Ct);
        var watchlist = await ReadAsync<PagedResponse<WatchlistItemResponse>>(response, HttpStatusCode.OK);
        Assert.Single(watchlist.Items);
    }

    [Fact]
    public async Task Create_defaults_to_no_rating_and_no_flags()
    {
        var movie = await CreateMovieAsync();

        var response = await Client.PostAsJsonAsync("/api/diary-entries", new { movieId = movie.Id, watchedOn = "2026-10-31" }, Ct);

        var entry = await ReadAsync<DiaryEntryResponse>(response, HttpStatusCode.Created);
        Assert.Equal((null, null, false, false), (entry.Rating, entry.Review, entry.IsRewatch, entry.Liked));
    }

    [Fact]
    public async Task Create_accepts_tomorrow_because_the_client_may_be_ahead_of_utc()
    {
        var movie = await CreateMovieAsync();

        var entry = await CreateDiaryEntryAsync(movie.Id, watchedOn: "2026-11-04");

        Assert.Equal(new DateOnly(2026, 11, 4), entry.WatchedOn);
    }

    [Fact]
    public async Task Create_rejects_a_date_in_the_future()
    {
        var movie = await CreateMovieAsync();

        var response = await Client.PostAsJsonAsync("/api/diary-entries", new { movieId = movie.Id, watchedOn = "2026-11-05" }, Ct);

        var problem = await ReadValidationProblemAsync(response);
        Assert.Contains("watchedOn", problem.Errors.Keys);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    public async Task Create_rejects_a_rating_outside_half_stars_1_to_10(int rating)
    {
        var movie = await CreateMovieAsync();

        var response = await Client.PostAsJsonAsync("/api/diary-entries", new { movieId = movie.Id, watchedOn = "2026-10-31", rating }, Ct);

        var problem = await ReadValidationProblemAsync(response);
        Assert.Contains("rating", problem.Errors.Keys);
    }

    [Fact]
    public async Task Create_requires_movie_and_date()
    {
        var response = await Client.PostAsJsonAsync("/api/diary-entries", new { rating = 5 }, Ct);

        var problem = await ReadValidationProblemAsync(response);
        Assert.Contains("movieId", problem.Errors.Keys);
        Assert.Contains("watchedOn", problem.Errors.Keys);
    }

    [Fact]
    public async Task Create_returns_422_for_an_unknown_movie()
    {
        var response = await Client.PostAsJsonAsync("/api/diary-entries", new { movieId = Guid.NewGuid(), watchedOn = "2026-10-31" }, Ct);

        var problem = await ReadProblemAsync(response, HttpStatusCode.UnprocessableEntity);
        Assert.Equal("Unknown movie", problem.Title);
    }

    [Fact]
    public async Task Get_returns_the_entry()
    {
        var movie = await CreateMovieAsync();
        var created = await CreateDiaryEntryAsync(movie.Id, review: "Great");

        var entry = await GetAsync<DiaryEntryResponse>($"/api/diary-entries/{created.Id}");

        Assert.Equal(created, entry);
    }

    [Fact]
    public async Task Get_returns_404_for_an_unknown_entry()
    {
        var response = await Client.GetAsync($"/api/diary-entries/{Guid.NewGuid()}", Ct);

        await ReadProblemAsync(response, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task List_returns_entries_newest_first()
    {
        var movie = await CreateMovieAsync();
        await CreateDiaryEntryAsync(movie.Id, watchedOn: "2026-10-01", review: "first");
        await CreateDiaryEntryAsync(movie.Id, watchedOn: "2026-10-15", review: "second");
        Factory.Clock.Advance(TimeSpan.FromMinutes(1));
        await CreateDiaryEntryAsync(movie.Id, watchedOn: "2026-10-15", review: "logged later");

        var result = await GetAsync<PagedResponse<DiaryEntryResponse>>("/api/diary-entries");

        // Same day: the entry logged last comes first.
        Assert.Equal(["logged later", "second", "first"], result.Items.Select(entry => entry.Review));
        Assert.Equal(3, result.TotalCount);
    }

    [Fact]
    public async Task List_filters_by_period_and_movie()
    {
        var alien = await CreateMovieAsync("Alien");
        var heat = await CreateMovieAsync("Heat");
        await CreateDiaryEntryAsync(alien.Id, watchedOn: "2026-09-30");
        await CreateDiaryEntryAsync(alien.Id, watchedOn: "2026-10-01");
        await CreateDiaryEntryAsync(heat.Id, watchedOn: "2026-10-31");
        await CreateDiaryEntryAsync(alien.Id, watchedOn: "2026-11-01");

        var october = await GetAsync<PagedResponse<DiaryEntryResponse>>("/api/diary-entries?from=2026-10-01&to=2026-10-31");
        var alienOnly = await GetAsync<PagedResponse<DiaryEntryResponse>>($"/api/diary-entries?movieId={alien.Id}&from=2026-10-01");

        Assert.Equal([new DateOnly(2026, 10, 31), new DateOnly(2026, 10, 1)], october.Items.Select(entry => entry.WatchedOn));
        Assert.Equal([new DateOnly(2026, 11, 1), new DateOnly(2026, 10, 1)], alienOnly.Items.Select(entry => entry.WatchedOn));
    }

    [Fact]
    public async Task List_pages_through_the_entries()
    {
        var movie = await CreateMovieAsync();
        for (var day = 1; day <= 5; day++)
        {
            await CreateDiaryEntryAsync(movie.Id, watchedOn: $"2026-10-0{day}");
        }

        var page2 = await GetAsync<PagedResponse<DiaryEntryResponse>>("/api/diary-entries?page=2&pageSize=2");

        Assert.Equal([new DateOnly(2026, 10, 3), new DateOnly(2026, 10, 2)], page2.Items.Select(entry => entry.WatchedOn));
        Assert.Equal(5, page2.TotalCount);
    }

    [Fact]
    public async Task List_rejects_a_period_that_ends_before_it_starts()
    {
        var response = await Client.GetAsync("/api/diary-entries?from=2026-10-31&to=2026-10-01", Ct);

        var problem = await ReadValidationProblemAsync(response);
        Assert.Contains("from", problem.Errors.Keys);
    }

    [Theory]
    [InlineData("page=0")]
    [InlineData("pageSize=101")]
    [InlineData("from=yesterday")]
    public async Task List_rejects_invalid_query_parameters(string query)
    {
        var response = await Client.GetAsync($"/api/diary-entries?{query}", Ct);

        await ReadValidationProblemAsync(response);
    }

    [Fact]
    public async Task Update_replaces_the_entry()
    {
        var alien = await CreateMovieAsync("Alien");
        var heat = await CreateMovieAsync("Heat");
        var entry = await CreateDiaryEntryAsync(alien.Id, rating: 9, review: "Great");

        var response = await Client.PutAsJsonAsync($"/api/diary-entries/{entry.Id}", new
        {
            movieId = heat.Id,
            watchedOn = "2026-11-01",
            liked = true,
        }, Ct);

        var updated = await ReadAsync<DiaryEntryResponse>(response, HttpStatusCode.OK);
        Assert.Equal(heat.Id, updated.Movie.Id);
        Assert.Equal(new DateOnly(2026, 11, 1), updated.WatchedOn);
        Assert.Null(updated.Rating);
        Assert.Null(updated.Review);
        Assert.True(updated.Liked);
        Assert.Equal(entry.CreatedAt, updated.CreatedAt);
        Assert.Equal(updated, await GetAsync<DiaryEntryResponse>($"/api/diary-entries/{entry.Id}"));
    }

    [Fact]
    public async Task Update_returns_404_for_an_unknown_entry()
    {
        var movie = await CreateMovieAsync();

        var response = await Client.PutAsJsonAsync($"/api/diary-entries/{Guid.NewGuid()}", new { movieId = movie.Id, watchedOn = "2026-10-31" }, Ct);

        await ReadProblemAsync(response, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Update_returns_422_for_an_unknown_movie()
    {
        var movie = await CreateMovieAsync();
        var entry = await CreateDiaryEntryAsync(movie.Id);

        var response = await Client.PutAsJsonAsync($"/api/diary-entries/{entry.Id}", new { movieId = Guid.NewGuid(), watchedOn = "2026-10-31" }, Ct);

        await ReadProblemAsync(response, HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Update_rejects_a_date_in_the_future()
    {
        var movie = await CreateMovieAsync();
        var entry = await CreateDiaryEntryAsync(movie.Id);

        var response = await Client.PutAsJsonAsync($"/api/diary-entries/{entry.Id}", new { movieId = movie.Id, watchedOn = "2027-01-01" }, Ct);

        await ReadValidationProblemAsync(response);
    }

    [Fact]
    public async Task Delete_removes_the_entry()
    {
        var movie = await CreateMovieAsync();
        var entry = await CreateDiaryEntryAsync(movie.Id);

        var response = await Client.DeleteAsync($"/api/diary-entries/{entry.Id}", Ct);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync($"/api/diary-entries/{entry.Id}", Ct)).StatusCode);
        Assert.Equal(0, (await GetAsync<MovieResponse>($"/api/movies/{movie.Id}")).WatchCount);
    }

    [Fact]
    public async Task Delete_returns_404_for_an_unknown_entry()
    {
        var response = await Client.DeleteAsync($"/api/diary-entries/{Guid.NewGuid()}", Ct);

        await ReadProblemAsync(response, HttpStatusCode.NotFound);
    }
}
