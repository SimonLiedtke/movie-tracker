using System.Net;
using System.Net.Http.Json;
using MovieTracker.Api.Contracts;
using MovieTracker.Api.Tests.Infrastructure;

namespace MovieTracker.Api.Tests;

public sealed class TagsTests(ApiFactory factory) : ApiTestBase(factory)
{
    private async Task<WatchlistItemResponse> PutWatchlistAsync(Guid movieId, string[]? tags, HttpClient? client = null)
    {
        var response = await (client ?? Client).PutAsJsonAsync($"/api/watchlist/{movieId}", new { tags }, Ct);
        return await ReadAsync<WatchlistItemResponse>(response, HttpStatusCode.Created);
    }

    // --- Diary entries

    [Fact]
    public async Task A_diary_entry_has_no_tags_by_default()
    {
        var movie = await CreateMovieAsync();

        var entry = await CreateDiaryEntryAsync(movie.Id);

        Assert.Empty(entry.Tags);
    }

    [Fact]
    public async Task A_diary_entry_keeps_its_tags_normalized()
    {
        var movie = await CreateMovieAsync();

        var created = await CreateDiaryEntryAsync(movie.Id, tags: ["Date Night", " horror ", "HORROR", ""]);

        Assert.Equal(["date night", "horror"], created.Tags);
        var fetched = await GetAsync<DiaryEntryResponse>($"/api/diary-entries/{created.Id}");
        Assert.Equal(["date night", "horror"], fetched.Tags);
    }

    [Fact]
    public async Task Updating_a_diary_entry_replaces_its_tags()
    {
        var movie = await CreateMovieAsync();
        var created = await CreateDiaryEntryAsync(movie.Id, tags: ["a", "b"]);

        var response = await Client.PutAsJsonAsync(
            $"/api/diary-entries/{created.Id}",
            new { movieId = movie.Id, watchedOn = "2026-10-31", tags = new[] { "c" } },
            Ct);

        Assert.Equal(["c"], (await ReadAsync<DiaryEntryResponse>(response, HttpStatusCode.OK)).Tags);
    }

    // --- Watchlist

    [Fact]
    public async Task A_watchlist_item_has_no_tags_by_default()
    {
        var movie = await CreateMovieAsync();

        var response = await Client.PutAsync($"/api/watchlist/{movie.Id}", content: null, Ct);

        Assert.Empty((await ReadAsync<WatchlistItemResponse>(response, HttpStatusCode.Created)).Tags);
    }

    [Fact]
    public async Task A_watchlist_item_keeps_its_tags_normalized()
    {
        var movie = await CreateMovieAsync();

        var item = await PutWatchlistAsync(movie.Id, ["Date Night", "horror", "Horror"]);

        Assert.Equal(["date night", "horror"], item.Tags);
        var list = await GetAsync<PagedResponse<WatchlistItemResponse>>("/api/watchlist");
        Assert.Equal(["date night", "horror"], Assert.Single(list.Items).Tags);
    }

    [Fact]
    public async Task Putting_a_watchlist_item_again_replaces_its_tags()
    {
        var movie = await CreateMovieAsync();
        await PutWatchlistAsync(movie.Id, ["a", "b"]);

        var response = await Client.PutAsJsonAsync($"/api/watchlist/{movie.Id}", new { tags = new[] { "c" } }, Ct);

        Assert.Equal(["c"], (await ReadAsync<WatchlistItemResponse>(response, HttpStatusCode.OK)).Tags);
    }

    // --- From the watchlist to the diary

    [Fact]
    public async Task Logging_a_viewing_keeps_the_tags_of_the_watchlist_item()
    {
        var movie = await CreateMovieAsync();
        await PutWatchlistAsync(movie.Id, ["recommended", "horror"]);

        var entry = await CreateDiaryEntryAsync(movie.Id, tags: ["cinema", "Horror"]);

        Assert.Equal(["cinema", "horror", "recommended"], entry.Tags);
        var watchlist = await GetAsync<PagedResponse<WatchlistItemResponse>>("/api/watchlist");
        Assert.Empty(watchlist.Items);
    }

    [Fact]
    public async Task Logging_a_viewing_without_tags_keeps_the_tags_of_the_watchlist_item()
    {
        var movie = await CreateMovieAsync();
        await PutWatchlistAsync(movie.Id, ["recommended"]);

        var entry = await CreateDiaryEntryAsync(movie.Id);

        Assert.Equal(["recommended"], entry.Tags);
    }

    [Fact]
    public async Task Logging_a_viewing_does_not_take_over_the_tags_of_the_watchlist_of_another_user()
    {
        var movie = await ImportTmdbMovieAsync();
        await PutWatchlistAsync(movie.Id, ["bobs tag"], Bob);

        var entry = await CreateDiaryEntryAsync(movie.Id);

        Assert.Empty(entry.Tags);
    }
}
