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
