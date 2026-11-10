using System.Net;
using System.Net.Http.Json;
using MovieTracker.Api.Contracts;
using MovieTracker.Api.Tests.Infrastructure;

namespace MovieTracker.Api.Tests;

public sealed class WatchlistTests(ApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task Put_adds_a_movie_and_returns_201()
    {
        var movie = await CreateMovieAsync();

        var response = await Client.PutAsJsonAsync($"/api/watchlist/{movie.Id}", new { note = "Director's cut" }, Ct);

        var item = await ReadAsync<WatchlistItemResponse>(response, HttpStatusCode.Created);
        Assert.Equal(movie.Id, item.Movie.Id);
        Assert.Equal("Director's cut", item.Note);
        Assert.Equal(ApiFactory.StartTime, item.AddedAt);
    }

    [Fact]
    public async Task Put_accepts_an_empty_body()
    {
        var movie = await CreateMovieAsync();

        var response = await Client.PutAsync($"/api/watchlist/{movie.Id}", content: null, Ct);

        var item = await ReadAsync<WatchlistItemResponse>(response, HttpStatusCode.Created);
        Assert.Null(item.Note);
    }

    [Fact]
    public async Task Put_again_updates_the_note_and_keeps_the_date_added()
    {
        var movie = await CreateMovieAsync();
        await Client.PutAsJsonAsync($"/api/watchlist/{movie.Id}", new { note = "first" }, Ct);
        Factory.Clock.Advance(TimeSpan.FromDays(1));

        var response = await Client.PutAsJsonAsync($"/api/watchlist/{movie.Id}", new { note = "second" }, Ct);

        var item = await ReadAsync<WatchlistItemResponse>(response, HttpStatusCode.OK);
        Assert.Equal("second", item.Note);
        Assert.Equal(ApiFactory.StartTime, item.AddedAt);
    }

    [Fact]
    public async Task Put_returns_404_for_an_unknown_movie()
    {
        var response = await Client.PutAsJsonAsync($"/api/watchlist/{Guid.NewGuid()}", new { note = "?" }, Ct);

        await ReadProblemAsync(response, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Put_rejects_a_note_longer_than_500_characters()
    {
        var movie = await CreateMovieAsync();

        var response = await Client.PutAsJsonAsync($"/api/watchlist/{movie.Id}", new { note = new string('x', 501) }, Ct);

        var problem = await ReadValidationProblemAsync(response);
        Assert.Contains("note", problem.Errors.Keys);
    }

    [Fact]
    public async Task List_returns_the_most_recently_added_first()
    {
        var alien = await CreateMovieAsync("Alien");
        var heat = await CreateMovieAsync("Heat");
        await Client.PutAsJsonAsync($"/api/watchlist/{alien.Id}", new { }, Ct);
        Factory.Clock.Advance(TimeSpan.FromMinutes(1));
        await Client.PutAsJsonAsync($"/api/watchlist/{heat.Id}", new { }, Ct);

        var result = await GetAsync<PagedResponse<WatchlistItemResponse>>("/api/watchlist");

        Assert.Equal(["Heat", "Alien"], result.Items.Select(item => item.Movie.Title));
        Assert.Equal(2, result.TotalCount);
    }

    [Fact]
    public async Task List_pages_through_the_items()
    {
        foreach (var title in new[] { "A", "B", "C" })
        {
            var movie = await CreateMovieAsync(title);
            await Client.PutAsJsonAsync($"/api/watchlist/{movie.Id}", new { }, Ct);
            Factory.Clock.Advance(TimeSpan.FromMinutes(1));
        }

        var page2 = await GetAsync<PagedResponse<WatchlistItemResponse>>("/api/watchlist?page=2&pageSize=2");

        Assert.Equal(["A"], page2.Items.Select(item => item.Movie.Title));
        Assert.Equal(3, page2.TotalCount);
    }

    private async Task<List<string>> ListTitlesAsync(string query)
    {
        var result = await GetAsync<PagedResponse<WatchlistItemResponse>>($"/api/watchlist{query}");
        return result.Items.Select(item => item.Movie.Title).Order().ToList();
    }

    /// <summary>Alien: horror, sci-fi. Halloween: horror. Solaris: sci-fi, drama. Heat: drama. Paddington: no genre.</summary>
    private async Task PutGenreMoviesOnTheWatchlistAsync()
    {
        foreach (var (title, genreIds) in new (string, int[])[]
        {
            ("Alien", [Horror, ScienceFiction]),
            ("Halloween", [Horror]),
            ("Solaris", [ScienceFiction, Drama]),
            ("Heat", [Drama]),
            ("Paddington", []),
        })
        {
            var movie = await CreateMovieAsync(title, genreIds: genreIds);
            await Client.PutAsJsonAsync($"/api/watchlist/{movie.Id}", new { }, Ct);
        }
    }

    [Fact]
    public async Task List_and_put_include_the_genres_of_the_movie_alphabetically()
    {
        var movie = await CreateMovieAsync(genreIds: [ScienceFiction, Horror]);
        var put = await Client.PutAsJsonAsync($"/api/watchlist/{movie.Id}", new { }, Ct);

        var created = await ReadAsync<WatchlistItemResponse>(put, HttpStatusCode.Created);
        var listed = (await GetAsync<PagedResponse<WatchlistItemResponse>>("/api/watchlist")).Items.Single();

        Assert.Equal(["Horror", "Science Fiction"], created.Genres.Select(genre => genre.Name));
        Assert.Equal(created.Genres, listed.Genres);
    }

    [Fact]
    public async Task List_with_one_genre_returns_the_movies_with_that_genre()
    {
        await PutGenreMoviesOnTheWatchlistAsync();

        Assert.Equal(["Alien", "Halloween"], await ListTitlesAsync($"?genreId={Horror}"));
    }

    [Fact]
    public async Task List_with_several_genres_returns_the_movies_with_any_of_them_by_default()
    {
        await PutGenreMoviesOnTheWatchlistAsync();

        var expected = new[] { "Alien", "Halloween", "Solaris" };
        Assert.Equal(expected, await ListTitlesAsync($"?genreId={Horror}&genreId={ScienceFiction}"));
        Assert.Equal(expected, await ListTitlesAsync($"?genreId={Horror}&genreId={ScienceFiction}&genreMatch=any"));
    }

    [Fact]
    public async Task List_with_genreMatch_all_returns_only_the_movies_with_every_genre()
    {
        await PutGenreMoviesOnTheWatchlistAsync();

        Assert.Equal(["Alien"], await ListTitlesAsync($"?genreId={Horror}&genreId={ScienceFiction}&genreMatch=all"));
        Assert.Equal(["Solaris"], await ListTitlesAsync($"?genreId={Drama}&genreId={ScienceFiction}&genreMatch=all"));
        Assert.Empty(await ListTitlesAsync($"?genreId={Horror}&genreId={Drama}&genreMatch=all"));
    }

    [Fact]
    public async Task List_with_a_repeated_genre_counts_it_once()
    {
        await PutGenreMoviesOnTheWatchlistAsync();

        Assert.Equal(
            ["Alien", "Halloween"], await ListTitlesAsync($"?genreId={Horror}&genreId={Horror}&genreMatch=all"));
    }

    [Fact]
    public async Task List_with_an_unknown_genre_matches_nothing_for_all_and_is_ignored_for_any()
    {
        await PutGenreMoviesOnTheWatchlistAsync();

        Assert.Empty(await ListTitlesAsync($"?genreId={Horror}&genreId=99999&genreMatch=all"));
        Assert.Equal(["Alien", "Halloween"], await ListTitlesAsync($"?genreId={Horror}&genreId=99999"));
    }

    [Fact]
    public async Task List_counts_and_pages_only_the_movies_matching_the_genres()
    {
        await PutGenreMoviesOnTheWatchlistAsync();

        var result = await GetAsync<PagedResponse<WatchlistItemResponse>>(
            $"/api/watchlist?genreId={Horror}&genreId={ScienceFiction}&pageSize=2&page=2");

        Assert.Equal(3, result.TotalCount);
        Assert.Single(result.Items);
    }

    [Fact]
    public async Task List_returns_the_number_of_all_items_whatever_the_filter()
    {
        await PutGenreMoviesOnTheWatchlistAsync();

        var unfiltered = await GetAsync<WatchlistPageResponse>("/api/watchlist");
        var horror = await GetAsync<WatchlistPageResponse>($"/api/watchlist?genreId={Horror}");
        var none = await GetAsync<WatchlistPageResponse>($"/api/watchlist?genreId={Horror}&genreId={Drama}&genreMatch=all");
        var page2 = await GetAsync<WatchlistPageResponse>($"/api/watchlist?genreId={Horror}&pageSize=1&page=2");

        Assert.Equal((5, 5), (unfiltered.TotalCount, unfiltered.UnfilteredCount));
        Assert.Equal((2, 5), (horror.TotalCount, horror.UnfilteredCount));
        Assert.Equal((0, 5), (none.TotalCount, none.UnfilteredCount));
        Assert.Equal((2, 5), (page2.TotalCount, page2.UnfilteredCount));
    }

    [Fact]
    public async Task List_does_not_count_other_users_items_as_unfiltered()
    {
        await PutGenreMoviesOnTheWatchlistAsync();

        var result = await Bob.GetFromJsonAsync<WatchlistPageResponse>($"/api/watchlist?genreId={Horror}", Ct);

        Assert.Equal((0, 0), (result!.TotalCount, result.UnfilteredCount));
    }

    [Fact]
    public async Task List_with_genres_does_not_show_other_users_items()
    {
        await PutGenreMoviesOnTheWatchlistAsync();

        var result = await Bob.GetFromJsonAsync<PagedResponse<WatchlistItemResponse>>(
            $"/api/watchlist?genreId={Horror}", Ct);

        Assert.Empty(result!.Items);
    }

    /// <summary>Alien 1979-05-25 (horror), Heat 1995-12-15 (drama), Solaris 1972-03-20 (sci-fi), Unreleased: no date.</summary>
    private async Task PutDatedMoviesOnTheWatchlistAsync()
    {
        foreach (var (title, releaseDate, genreId) in new (string, string?, int)[]
        {
            ("Alien", "1979-05-25", Horror),
            ("Heat", "1995-12-15", Drama),
            ("Solaris", "1972-03-20", ScienceFiction),
            ("Unreleased", null, Horror),
        })
        {
            var movie = await CreateMovieAsync(title, releaseDate, genreIds: [genreId]);
            await Client.PutAsJsonAsync($"/api/watchlist/{movie.Id}", new { }, Ct);
        }
    }

    [Fact]
    public async Task List_with_releasedFrom_returns_the_movies_released_on_or_after_that_day()
    {
        await PutDatedMoviesOnTheWatchlistAsync();

        Assert.Equal(["Alien", "Heat"], await ListTitlesAsync("?releasedFrom=1979-05-25"));
        Assert.Equal(["Heat"], await ListTitlesAsync("?releasedFrom=1979-05-26"));
    }

    [Fact]
    public async Task List_with_releasedTo_returns_the_movies_released_on_or_before_that_day()
    {
        await PutDatedMoviesOnTheWatchlistAsync();

        Assert.Equal(["Alien", "Solaris"], await ListTitlesAsync("?releasedTo=1979-05-25"));
        Assert.Equal(["Solaris"], await ListTitlesAsync("?releasedTo=1979-05-24"));
    }

    [Fact]
    public async Task List_with_both_release_dates_returns_the_movies_released_in_the_period_and_not_those_without_a_date()
    {
        await PutDatedMoviesOnTheWatchlistAsync();

        Assert.Equal(["Alien", "Solaris"], await ListTitlesAsync("?releasedFrom=1972-03-20&releasedTo=1979-05-25"));
        Assert.Equal(["Heat"], await ListTitlesAsync("?releasedFrom=1995-12-15&releasedTo=1995-12-15"));
        // Without a date filter, the movie without release date is listed.
        Assert.Contains("Unreleased", await ListTitlesAsync(""));
    }

    [Fact]
    public async Task List_combines_the_release_dates_with_the_genres()
    {
        await PutDatedMoviesOnTheWatchlistAsync();

        Assert.Equal(["Alien"], await ListTitlesAsync($"?genreId={Horror}&releasedFrom=1970-01-01"));
        Assert.Empty(await ListTitlesAsync($"?genreId={Drama}&releasedTo=1990-01-01"));
    }

    [Fact]
    public async Task List_counts_all_items_as_unfiltered_when_only_the_release_date_filters()
    {
        await PutDatedMoviesOnTheWatchlistAsync();

        var result = await GetAsync<WatchlistPageResponse>("/api/watchlist?releasedFrom=1990-01-01");

        Assert.Equal((1, 4), (result.TotalCount, result.UnfilteredCount));
    }

    /// <summary>
    /// Added on 2026-11-03 (12:00), 2026-11-03 (23:59:59), 2026-11-04 (00:00) and 2026-11-10 (12:00), all UTC.
    /// </summary>
    private async Task PutMoviesOnTheWatchlistOverSeveralDaysAsync()
    {
        foreach (var (title, advance) in new (string, TimeSpan)[]
        {
            ("Monday noon", TimeSpan.Zero),
            ("Monday night", new TimeSpan(11, 59, 59)),
            ("Tuesday midnight", TimeSpan.FromSeconds(1)),
            ("Next week", new TimeSpan(6, 12, 0, 0)),
        })
        {
            Factory.Clock.Advance(advance);
            var movie = await CreateMovieAsync(title);
            await Client.PutAsJsonAsync($"/api/watchlist/{movie.Id}", new { }, Ct);
        }
    }

    [Fact]
    public async Task List_with_addedFrom_returns_the_items_added_on_or_after_the_start_of_that_day()
    {
        await PutMoviesOnTheWatchlistOverSeveralDaysAsync();

        Assert.Equal(["Monday night", "Monday noon", "Next week", "Tuesday midnight"], await ListTitlesAsync("?addedFrom=2026-11-03"));
        Assert.Equal(["Next week", "Tuesday midnight"], await ListTitlesAsync("?addedFrom=2026-11-04"));
        Assert.Equal(["Next week"], await ListTitlesAsync("?addedFrom=2026-11-05"));
    }

    [Fact]
    public async Task List_with_addedTo_returns_the_items_added_on_or_before_the_end_of_that_day()
    {
        await PutMoviesOnTheWatchlistOverSeveralDaysAsync();

        Assert.Equal(["Monday night", "Monday noon"], await ListTitlesAsync("?addedTo=2026-11-03"));
        Assert.Equal(["Monday night", "Monday noon", "Tuesday midnight"], await ListTitlesAsync("?addedTo=2026-11-04"));
        Assert.Empty(await ListTitlesAsync("?addedTo=2026-11-02"));
    }

    [Fact]
    public async Task List_with_both_added_dates_returns_the_items_added_in_the_period()
    {
        await PutMoviesOnTheWatchlistOverSeveralDaysAsync();

        Assert.Equal(["Tuesday midnight"], await ListTitlesAsync("?addedFrom=2026-11-04&addedTo=2026-11-04"));
        Assert.Equal(
            ["Monday night", "Monday noon", "Tuesday midnight"], await ListTitlesAsync("?addedFrom=2026-11-03&addedTo=2026-11-09"));
    }

    [Fact]
    public async Task List_accepts_the_last_possible_day_as_addedTo()
    {
        await PutMoviesOnTheWatchlistOverSeveralDaysAsync();

        Assert.Equal(4, (await ListTitlesAsync("?addedTo=9999-12-31")).Count);
    }

    [Fact]
    public async Task List_combines_the_added_dates_with_the_other_filters()
    {
        await PutDatedMoviesOnTheWatchlistAsync();
        Factory.Clock.Advance(TimeSpan.FromDays(5));
        var late = await CreateMovieAsync("Late", "1979-01-01", genreIds: [Horror]);
        await Client.PutAsJsonAsync($"/api/watchlist/{late.Id}", new { }, Ct);

        Assert.Equal(["Late"], await ListTitlesAsync($"?genreId={Horror}&addedFrom=2026-11-08"));
        Assert.Equal(["Alien"], await ListTitlesAsync($"?genreId={Horror}&addedTo=2026-11-07&releasedFrom=1970-01-01"));
        var result = await GetAsync<WatchlistPageResponse>("/api/watchlist?addedFrom=2026-11-08");
        Assert.Equal((1, 5), (result.TotalCount, result.UnfilteredCount));
    }

    [Fact]
    public async Task List_rejects_an_added_period_that_ends_before_it_starts()
    {
        var response = await Client.GetAsync("/api/watchlist?addedFrom=2026-11-04&addedTo=2026-11-03", Ct);

        var problem = await ReadValidationProblemAsync(response);
        Assert.Contains("addedFrom", problem.Errors.Keys);
    }

    [Fact]
    public async Task List_rejects_an_invalid_added_date()
    {
        var response = await Client.GetAsync("/api/watchlist?addedTo=tomorrow", Ct);

        var problem = await ReadValidationProblemAsync(response);
        Assert.Contains("addedTo", problem.Errors.Keys);
    }

    [Fact]
    public async Task List_rejects_a_release_period_that_ends_before_it_starts()
    {
        var response = await Client.GetAsync("/api/watchlist?releasedFrom=1990-01-01&releasedTo=1980-01-01", Ct);

        var problem = await ReadValidationProblemAsync(response);
        Assert.Contains("releasedFrom", problem.Errors.Keys);
    }

    [Fact]
    public async Task List_rejects_an_invalid_release_date()
    {
        var response = await Client.GetAsync("/api/watchlist?releasedFrom=yesterday", Ct);

        var problem = await ReadValidationProblemAsync(response);
        Assert.Contains("releasedFrom", problem.Errors.Keys);
    }

    [Fact]
    public async Task List_rejects_an_unknown_genreMatch()
    {
        var response = await Client.GetAsync("/api/watchlist?genreMatch=some", Ct);

        var problem = await ReadValidationProblemAsync(response);
        Assert.Contains("genreMatch", problem.Errors.Keys);
    }

    [Fact]
    public async Task List_rejects_an_invalid_page_size()
    {
        var response = await Client.GetAsync("/api/watchlist?pageSize=101", Ct);

        await ReadValidationProblemAsync(response);
    }

    [Fact]
    public async Task Delete_removes_the_movie_from_the_watchlist()
    {
        var movie = await CreateMovieAsync();
        await Client.PutAsJsonAsync($"/api/watchlist/{movie.Id}", new { }, Ct);

        var response = await Client.DeleteAsync($"/api/watchlist/{movie.Id}", Ct);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty((await GetAsync<PagedResponse<WatchlistItemResponse>>("/api/watchlist")).Items);
        Assert.False((await GetAsync<MovieResponse>($"/api/movies/{movie.Id}")).OnWatchlist);
    }

    [Fact]
    public async Task Delete_returns_404_if_the_movie_is_not_on_the_watchlist()
    {
        var movie = await CreateMovieAsync();

        var response = await Client.DeleteAsync($"/api/watchlist/{movie.Id}", Ct);

        await ReadProblemAsync(response, HttpStatusCode.NotFound);
    }
}
