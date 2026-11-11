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

    [Fact]
    public async Task Diary_entries_can_be_filtered_by_tag_whatever_its_case()
    {
        var movie = await CreateMovieAsync();
        await CreateDiaryEntryAsync(movie.Id, "2026-10-30", tags: ["horror", "cinema"]);
        await CreateDiaryEntryAsync(movie.Id, "2026-10-31", tags: ["comedy"]);
        await CreateDiaryEntryAsync(movie.Id, "2026-11-01");

        var result = await GetAsync<PagedResponse<DiaryEntryResponse>>("/api/diary-entries?tag=%20HORROR");

        var entry = Assert.Single(result.Items);
        Assert.Equal("2026-10-30", entry.WatchedOn.ToString("yyyy-MM-dd"));
        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task A_blank_tag_filter_does_not_filter()
    {
        var movie = await CreateMovieAsync();
        await CreateDiaryEntryAsync(movie.Id, "2026-10-30", tags: ["horror"]);
        await CreateDiaryEntryAsync(movie.Id, "2026-10-31");

        var result = await GetAsync<PagedResponse<DiaryEntryResponse>>("/api/diary-entries?tag=%20");

        Assert.Equal(2, result.TotalCount);
    }

    [Fact]
    public async Task A_tag_filter_does_not_find_the_entries_of_another_user()
    {
        var movie = await CreateMovieAsync();
        await CreateDiaryEntryAsync(movie.Id, tags: ["horror"]);

        var response = await Bob.GetAsync("/api/diary-entries?tag=horror", Ct);

        Assert.Equal(0, (await ReadAsync<PagedResponse<DiaryEntryResponse>>(response, HttpStatusCode.OK)).TotalCount);
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

    [Fact]
    public async Task The_watchlist_can_be_filtered_by_tag_together_with_other_filters()
    {
        var alien = await CreateMovieAsync("Alien", genreIds: [Horror]);
        var aliens = await CreateMovieAsync("Aliens", genreIds: [ScienceFiction]);
        var heat = await CreateMovieAsync("Heat", genreIds: [Drama]);
        await PutWatchlistAsync(alien.Id, ["weekend"]);
        await PutWatchlistAsync(aliens.Id, ["weekend"]);
        await PutWatchlistAsync(heat.Id, ["later"]);

        var result = await GetAsync<WatchlistPageResponse>($"/api/watchlist?tag=Weekend&genreId={Horror}");

        Assert.Equal(["Alien"], result.Items.Select(item => item.Movie.Title));
        Assert.Equal(3, result.UnfilteredCount);
    }

    [Fact]
    public async Task The_watchlist_lists_its_different_tags_alphabetically()
    {
        var alien = await ImportTmdbMovieAsync();
        var heat = await CreateMovieAsync("Heat");
        await PutWatchlistAsync(alien.Id, ["weekend", "horror"]);
        await PutWatchlistAsync(heat.Id, ["Weekend", "crime"]);
        var logged = await CreateMovieAsync("Aliens");
        await CreateDiaryEntryAsync(logged.Id, tags: ["not on the watchlist"]);

        var tags = await GetAsync<List<string>>("/api/watchlist/tags");

        Assert.Equal(["crime", "horror", "weekend"], tags);
    }

    [Fact]
    public async Task The_tags_of_the_watchlist_are_the_users_own()
    {
        var movie = await ImportTmdbMovieAsync();
        await PutWatchlistAsync(movie.Id, ["alices tag"]);

        var response = await Bob.GetAsync("/api/watchlist/tags", Ct);

        Assert.Empty(await ReadAsync<List<string>>(response, HttpStatusCode.OK));
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

    // --- Validation

    [Theory]
    [InlineData("diary")]
    [InlineData("watchlist")]
    public async Task More_than_20_different_tags_are_rejected(string target)
    {
        var movie = await CreateMovieAsync();
        var tags = Enumerable.Range(1, 21).Select(i => $"tag {i}").ToArray();

        var response = await SendAsync(target, movie.Id, tags);

        var problem = await ReadValidationProblemAsync(response);
        Assert.Contains("tags", problem.Errors.Keys);
    }

    [Theory]
    [InlineData("diary")]
    [InlineData("watchlist")]
    public async Task The_same_tag_in_different_cases_counts_once(string target)
    {
        var movie = await CreateMovieAsync();
        var tags = Enumerable.Range(1, 20).Select(i => $"tag {i}").Append("TAG 1").ToArray();

        var response = await SendAsync(target, movie.Id, tags);

        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync(Ct));
    }

    [Theory]
    [InlineData("diary")]
    [InlineData("watchlist")]
    public async Task A_tag_longer_than_50_characters_is_rejected(string target)
    {
        var movie = await CreateMovieAsync();

        var response = await SendAsync(target, movie.Id, [new string('x', 51)]);

        var problem = await ReadValidationProblemAsync(response);
        Assert.Contains("tags", problem.Errors.Keys);
    }

    [Theory]
    [InlineData("diary")]
    [InlineData("watchlist")]
    public async Task A_tag_with_a_comma_is_rejected(string target)
    {
        var movie = await CreateMovieAsync();

        var response = await SendAsync(target, movie.Id, ["a,b"]);

        var problem = await ReadValidationProblemAsync(response);
        Assert.Contains("tags", problem.Errors.Keys);
    }

    private Task<HttpResponseMessage> SendAsync(string target, Guid movieId, string[] tags) =>
        target == "diary"
            ? Client.PostAsJsonAsync("/api/diary-entries", new { movieId, watchedOn = "2026-10-31", tags }, Ct)
            : Client.PutAsJsonAsync($"/api/watchlist/{movieId}", new { tags }, Ct);
}
