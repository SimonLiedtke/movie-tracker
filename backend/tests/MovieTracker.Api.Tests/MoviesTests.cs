using System.Net;
using System.Net.Http.Json;
using MovieTracker.Api.Contracts;
using MovieTracker.Api.Tests.Infrastructure;

namespace MovieTracker.Api.Tests;

public sealed class MoviesTests(ApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task Create_returns_the_new_movie_with_its_location()
    {
        var response = await Client.PostAsJsonAsync("/api/movies", new
        {
            title = "  Alien  ",
            originalTitle = "Alien",
            releaseDate = "1979-05-25",
            runtimeMinutes = 117,
            overview = "In space no one can hear you scream.",
            posterUrl = "https://example.com/alien.jpg",
            tmdbId = 348,
            genreIds = new[] { ScienceFiction, Horror, Horror },
        }, Ct);

        var movie = await ReadAsync<MovieResponse>(response, HttpStatusCode.Created);
        Assert.Equal($"/api/movies/{movie.Id}", response.Headers.Location?.AbsolutePath);
        Assert.Equal("Alien", movie.Title);
        Assert.Equal(new DateOnly(1979, 5, 25), movie.ReleaseDate);
        Assert.Equal(117, movie.RuntimeMinutes);
        Assert.Equal(348, movie.TmdbId);
        Assert.Equal(["Horror", "Science Fiction"], movie.Genres.Select(genre => genre.Name));
        Assert.Null(movie.LatestRating);
        Assert.Equal(0, movie.WatchCount);
        Assert.False(movie.OnWatchlist);
        Assert.Null(movie.LastWatchedOn);
        Assert.Equal(ApiFactory.StartTime, movie.CreatedAt);
    }

    [Fact]
    public async Task Create_rejects_a_missing_title()
    {
        var response = await Client.PostAsJsonAsync("/api/movies", new { genreIds = Array.Empty<int>() }, Ct);

        var problem = await ReadValidationProblemAsync(response);
        Assert.Contains("title", problem.Errors.Keys);
    }

    [Fact]
    public async Task Create_rejects_unknown_properties()
    {
        var response = await Client.PostAsJsonAsync("/api/movies", new
        {
            title = "Alien",
            genreIds = Array.Empty<int>(),
            director = "Ridley Scott",
        }, Ct);

        await ReadValidationProblemAsync(response);
    }

    [Fact]
    public async Task Create_rejects_unknown_genres()
    {
        var response = await Client.PostAsJsonAsync("/api/movies", new { title = "Alien", genreIds = new[] { Horror, 99999 } }, Ct);

        var problem = await ReadValidationProblemAsync(response);
        Assert.Equal(["Unknown genre IDs: 99999."], problem.Errors["genreIds"]);
    }

    [Theory]
    [InlineData("runtimeMinutes", 0)]
    [InlineData("runtimeMinutes", 1501)]
    [InlineData("tmdbId", 0)]
    public async Task Create_rejects_out_of_range_numbers(string property, int value)
    {
        var body = new Dictionary<string, object> { ["title"] = "Alien", ["genreIds"] = Array.Empty<int>(), [property] = value };

        var response = await Client.PostAsJsonAsync("/api/movies", body, Ct);

        var problem = await ReadValidationProblemAsync(response);
        Assert.Contains(property, problem.Errors.Keys);
    }

    [Fact]
    public async Task Create_rejects_an_invalid_poster_url()
    {
        var response = await Client.PostAsJsonAsync("/api/movies", new { title = "Alien", posterUrl = "not a url", genreIds = Array.Empty<int>() }, Ct);

        var problem = await ReadValidationProblemAsync(response);
        Assert.Contains("posterUrl", problem.Errors.Keys);
    }

    [Fact]
    public async Task Create_rejects_a_duplicate_tmdb_id()
    {
        await CreateMovieAsync(tmdbId: 348);

        var response = await Client.PostAsJsonAsync("/api/movies", new { title = "Alien (copy)", tmdbId = 348, genreIds = Array.Empty<int>() }, Ct);

        var problem = await ReadProblemAsync(response, HttpStatusCode.Conflict);
        Assert.Equal("Duplicate TMDB ID", problem.Title);
    }

    [Fact]
    public async Task Get_returns_the_movie()
    {
        var created = await CreateMovieAsync();

        var movie = await GetAsync<MovieResponse>($"/api/movies/{created.Id}");

        Assert.Equal(created, movie with { Genres = created.Genres });
        Assert.Equal(created.Genres, movie.Genres);
    }

    [Fact]
    public async Task Get_returns_404_for_an_unknown_movie()
    {
        var response = await Client.GetAsync($"/api/movies/{Guid.NewGuid()}", Ct);

        await ReadProblemAsync(response, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_includes_viewing_statistics()
    {
        var movie = await CreateMovieAsync();
        await CreateDiaryEntryAsync(movie.Id, watchedOn: "2026-10-01", rating: 6);
        await CreateDiaryEntryAsync(movie.Id, watchedOn: "2026-10-20", rating: null);
        await CreateDiaryEntryAsync(movie.Id, watchedOn: "2026-09-01", rating: 10);
        await Client.PutAsJsonAsync($"/api/watchlist/{movie.Id}", new { note = (string?)null }, Ct);

        var result = await GetAsync<MovieResponse>($"/api/movies/{movie.Id}");

        Assert.Equal(6, result.LatestRating); // the most recent viewing with a rating
        Assert.Equal(3, result.WatchCount);
        Assert.Equal(new DateOnly(2026, 10, 20), result.LastWatchedOn);
        Assert.True(result.OnWatchlist);
    }

    [Fact]
    public async Task List_searches_title_and_original_title_case_insensitively()
    {
        await CreateMovieAsync("Alien");
        await CreateMovieAsync("Spirited Away", originalTitle: "Sen to Chihiro no Kamikakushi");
        await CreateMovieAsync("Heat");

        var byTitle = await GetAsync<PagedResponse<MovieSummaryResponse>>("/api/movies?search=ALI");
        var byOriginalTitle = await GetAsync<PagedResponse<MovieSummaryResponse>>("/api/movies?search=chihiro");

        Assert.Equal(["Alien"], byTitle.Items.Select(movie => movie.Title));
        Assert.Equal(["Spirited Away"], byOriginalTitle.Items.Select(movie => movie.Title));
    }

    [Theory]
    [InlineData("100%", "100% Wolf")]
    [InlineData("a_b", "a_b")]
    [InlineData(@"AC\DC", @"AC\DC")]
    public async Task List_treats_wildcards_in_the_search_literally(string search, string expectedTitle)
    {
        await CreateMovieAsync("100% Wolf");
        await CreateMovieAsync("1000 Wolves");
        await CreateMovieAsync("a_b");
        await CreateMovieAsync("axb");
        await CreateMovieAsync(@"AC\DC");

        var result = await GetAsync<PagedResponse<MovieSummaryResponse>>($"/api/movies?search={Uri.EscapeDataString(search)}");

        Assert.Equal([expectedTitle], result.Items.Select(movie => movie.Title));
    }

    [Fact]
    public async Task List_filters_by_genre_and_year()
    {
        await CreateMovieAsync("Alien", releaseDate: "1979-05-25", genreIds: [Horror, ScienceFiction]);
        await CreateMovieAsync("Halloween", releaseDate: "1978-10-25", genreIds: [Horror]);
        await CreateMovieAsync("Kramer vs. Kramer", releaseDate: "1979-12-19", genreIds: [Drama]);

        var horror = await GetAsync<PagedResponse<MovieSummaryResponse>>($"/api/movies?genreId={Horror}");
        var from1979 = await GetAsync<PagedResponse<MovieSummaryResponse>>("/api/movies?year=1979");
        var horrorFrom1979 = await GetAsync<PagedResponse<MovieSummaryResponse>>($"/api/movies?genreId={Horror}&year=1979");

        Assert.Equal(["Alien", "Halloween"], horror.Items.Select(movie => movie.Title));
        Assert.Equal(["Alien", "Kramer vs. Kramer"], from1979.Items.Select(movie => movie.Title));
        Assert.Equal(["Alien"], horrorFrom1979.Items.Select(movie => movie.Title));
    }

    [Theory]
    [InlineData("title", new[] { "Alien", "Blade Runner", "Close Encounters" })]
    [InlineData("-title", new[] { "Close Encounters", "Blade Runner", "Alien" })]
    [InlineData("releaseDate", new[] { "Close Encounters", "Alien", "Blade Runner" })]
    [InlineData("-releaseDate", new[] { "Blade Runner", "Alien", "Close Encounters" })]
    [InlineData("createdAt", new[] { "Blade Runner", "Close Encounters", "Alien" })]
    [InlineData("-createdAt", new[] { "Alien", "Close Encounters", "Blade Runner" })]
    public async Task List_sorts(string sort, string[] expectedTitles)
    {
        await CreateMovieAsync("Blade Runner", releaseDate: "1982-06-25");
        Factory.Clock.Advance(TimeSpan.FromMinutes(1));
        await CreateMovieAsync("Close Encounters", releaseDate: "1977-11-16");
        Factory.Clock.Advance(TimeSpan.FromMinutes(1));
        await CreateMovieAsync("Alien", releaseDate: "1979-05-25");

        var result = await GetAsync<PagedResponse<MovieSummaryResponse>>($"/api/movies?sort={sort}");

        Assert.Equal(expectedTitles, result.Items.Select(movie => movie.Title));
    }

    [Fact]
    public async Task List_pages_through_the_results()
    {
        foreach (var title in new[] { "A", "B", "C", "D", "E" })
        {
            await CreateMovieAsync(title);
        }

        var page2 = await GetAsync<PagedResponse<MovieSummaryResponse>>("/api/movies?page=2&pageSize=2");
        var page3 = await GetAsync<PagedResponse<MovieSummaryResponse>>("/api/movies?page=3&pageSize=2");
        var beyond = await GetAsync<PagedResponse<MovieSummaryResponse>>("/api/movies?page=99&pageSize=2");

        Assert.Equal(["C", "D"], page2.Items.Select(movie => movie.Title));
        Assert.Equal((2, 2, 5), (page2.Page, page2.PageSize, page2.TotalCount));
        Assert.Equal(["E"], page3.Items.Select(movie => movie.Title));
        Assert.Empty(beyond.Items);
        Assert.Equal(5, beyond.TotalCount);
    }

    [Fact]
    public async Task List_includes_viewing_statistics()
    {
        var movie = await CreateMovieAsync();
        await CreateDiaryEntryAsync(movie.Id, rating: 9);
        await Client.PutAsJsonAsync($"/api/watchlist/{movie.Id}", new { }, Ct);

        var result = await GetAsync<PagedResponse<MovieSummaryResponse>>("/api/movies");

        var summary = Assert.Single(result.Items);
        Assert.Equal((9, 1, true), (summary.LatestRating, summary.WatchCount, summary.OnWatchlist));
        Assert.Equal(["Horror", "Science Fiction"], summary.Genres.Select(genre => genre.Name));
    }

    [Theory]
    [InlineData("sort=director")]
    [InlineData("page=0")]
    [InlineData("pageSize=0")]
    [InlineData("pageSize=101")]
    [InlineData("year=1869")]
    public async Task List_rejects_invalid_query_parameters(string query)
    {
        var response = await Client.GetAsync($"/api/movies?{query}", Ct);

        await ReadValidationProblemAsync(response);
    }

    [Fact]
    public async Task Update_replaces_the_movie()
    {
        var movie = await CreateMovieAsync(tmdbId: 348);

        var response = await Client.PutAsJsonAsync($"/api/movies/{movie.Id}", new
        {
            title = "Aliens",
            tmdbId = 348, // unchanged: not a conflict with itself
            genreIds = new[] { Drama },
        }, Ct);

        var updated = await ReadAsync<MovieResponse>(response, HttpStatusCode.OK);
        Assert.Equal("Aliens", updated.Title);
        Assert.Null(updated.ReleaseDate);
        Assert.Null(updated.RuntimeMinutes);
        Assert.Equal(["Drama"], updated.Genres.Select(genre => genre.Name));
        Assert.Equal(movie.CreatedAt, updated.CreatedAt);
        Assert.Equal(updated, await GetAsync<MovieResponse>($"/api/movies/{movie.Id}") with { Genres = updated.Genres });
    }

    [Fact]
    public async Task Update_rejects_the_tmdb_id_of_another_movie()
    {
        await CreateMovieAsync("Alien", tmdbId: 348);
        var other = await CreateMovieAsync("Aliens", tmdbId: 679);

        var response = await Client.PutAsJsonAsync($"/api/movies/{other.Id}", new { title = "Aliens", tmdbId = 348, genreIds = Array.Empty<int>() }, Ct);

        await ReadProblemAsync(response, HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Update_returns_404_for_an_unknown_movie()
    {
        var response = await Client.PutAsJsonAsync($"/api/movies/{Guid.NewGuid()}", new { title = "Alien", genreIds = Array.Empty<int>() }, Ct);

        await ReadProblemAsync(response, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_removes_the_movie_and_its_watchlist_item()
    {
        var movie = await CreateMovieAsync();
        await Client.PutAsJsonAsync($"/api/watchlist/{movie.Id}", new { note = "soon" }, Ct);

        var response = await Client.DeleteAsync($"/api/movies/{movie.Id}", Ct);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync($"/api/movies/{movie.Id}", Ct)).StatusCode);
        Assert.Empty((await GetAsync<PagedResponse<WatchlistItemResponse>>("/api/watchlist")).Items);
    }

    [Fact]
    public async Task Delete_is_refused_while_the_movie_has_diary_entries()
    {
        var movie = await CreateMovieAsync();
        await CreateDiaryEntryAsync(movie.Id);

        var response = await Client.DeleteAsync($"/api/movies/{movie.Id}", Ct);

        var problem = await ReadProblemAsync(response, HttpStatusCode.Conflict);
        Assert.Equal("Movie has diary entries", problem.Title);
        Assert.Equal(HttpStatusCode.OK, (await Client.GetAsync($"/api/movies/{movie.Id}", Ct)).StatusCode);
    }

    [Fact]
    public async Task Delete_returns_404_for_an_unknown_movie()
    {
        var response = await Client.DeleteAsync($"/api/movies/{Guid.NewGuid()}", Ct);

        await ReadProblemAsync(response, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Diary_entries_of_a_movie_are_listed_newest_first()
    {
        var movie = await CreateMovieAsync();
        var other = await CreateMovieAsync("Heat");
        await CreateDiaryEntryAsync(movie.Id, watchedOn: "2026-01-01");
        await CreateDiaryEntryAsync(movie.Id, watchedOn: "2026-06-01");
        await CreateDiaryEntryAsync(other.Id, watchedOn: "2026-03-01");

        var entries = await GetAsync<List<DiaryEntryResponse>>($"/api/movies/{movie.Id}/diary-entries");

        Assert.Equal([new DateOnly(2026, 6, 1), new DateOnly(2026, 1, 1)], entries.Select(entry => entry.WatchedOn));
    }

    [Fact]
    public async Task Diary_entries_of_an_unknown_movie_return_404()
    {
        var response = await Client.GetAsync($"/api/movies/{Guid.NewGuid()}/diary-entries", Ct);

        await ReadProblemAsync(response, HttpStatusCode.NotFound);
    }
}
