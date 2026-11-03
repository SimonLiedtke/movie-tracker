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
