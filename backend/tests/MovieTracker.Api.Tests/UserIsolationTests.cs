using System.Net;
using System.Net.Http.Json;
using MovieTracker.Api.Contracts;
using MovieTracker.Api.Tests.Infrastructure;

namespace MovieTracker.Api.Tests;

/// <summary>Users share the movie catalog, but not their diaries and watchlists.</summary>
public sealed class UserIsolationTests(ApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task Other_users_cannot_read_change_or_delete_a_diary_entry()
    {
        var movie = await CreateMovieAsync();
        var entry = await CreateDiaryEntryAsync(movie.Id, review: "Alice's review");
        var url = $"/api/diary-entries/{entry.Id}";

        var get = await Bob.GetAsync(url, Ct);
        var put = await Bob.PutAsJsonAsync(url, new { movieId = movie.Id, watchedOn = "2026-10-01", review = "Bob was here" }, Ct);
        var delete = await Bob.DeleteAsync(url, Ct);

        // 404, not 403: other users' entries do not even exist for Bob.
        await ReadProblemAsync(get, HttpStatusCode.NotFound);
        await ReadProblemAsync(put, HttpStatusCode.NotFound);
        await ReadProblemAsync(delete, HttpStatusCode.NotFound);
        Assert.Equal(entry, await GetAsync<DiaryEntryResponse>(url));
    }

    [Fact]
    public async Task Diaries_only_list_the_users_own_entries()
    {
        var movie = await CreateMovieAsync();
        await CreateDiaryEntryAsync(movie.Id, review: "Alice's review");
        var bobsEntry = await ReadAsync<DiaryEntryResponse>(
            await Bob.PostAsJsonAsync("/api/diary-entries", new { movieId = movie.Id, watchedOn = "2026-10-15", review = "Bob's review" }, Ct),
            HttpStatusCode.Created);

        var alicesDiary = await GetAsync<PagedResponse<DiaryEntryResponse>>($"/api/diary-entries?movieId={movie.Id}");
        var bobsDiary = await ReadAsync<PagedResponse<DiaryEntryResponse>>(await Bob.GetAsync("/api/diary-entries", Ct), HttpStatusCode.OK);
        var bobsMovieEntries = await ReadAsync<List<DiaryEntryResponse>>(
            await Bob.GetAsync($"/api/movies/{movie.Id}/diary-entries", Ct), HttpStatusCode.OK);

        Assert.Equal(["Alice's review"], alicesDiary.Items.Select(entry => entry.Review));
        Assert.Equal(1, alicesDiary.TotalCount);
        Assert.Equal([bobsEntry], bobsDiary.Items);
        Assert.Equal([bobsEntry], bobsMovieEntries);
    }

    [Fact]
    public async Task Movie_statistics_are_those_of_the_signed_in_user()
    {
        var movie = await CreateMovieAsync();
        await CreateDiaryEntryAsync(movie.Id, rating: 10);
        await Client.PutAsJsonAsync($"/api/watchlist/{movie.Id}", new { }, Ct);

        var detail = await ReadAsync<MovieResponse>(await Bob.GetAsync($"/api/movies/{movie.Id}", Ct), HttpStatusCode.OK);
        var list = await ReadAsync<PagedResponse<MovieSummaryResponse>>(await Bob.GetAsync("/api/movies", Ct), HttpStatusCode.OK);

        Assert.Equal((null, 0, false, null), (detail.LatestRating, detail.WatchCount, detail.OnWatchlist, detail.LastWatchedOn));
        var summary = Assert.Single(list.Items);
        Assert.Equal((null, 0, false), (summary.LatestRating, summary.WatchCount, summary.OnWatchlist));
        var own = await GetAsync<MovieResponse>($"/api/movies/{movie.Id}");
        Assert.Equal((10, 1, true), (own.LatestRating, own.WatchCount, own.OnWatchlist));
    }

    [Fact]
    public async Task Every_user_has_their_own_watchlist()
    {
        var movie = await CreateMovieAsync();
        await Client.PutAsJsonAsync($"/api/watchlist/{movie.Id}", new { note = "Alice's note" }, Ct);

        var bobsEmptyWatchlist = await ReadAsync<PagedResponse<WatchlistItemResponse>>(await Bob.GetAsync("/api/watchlist", Ct), HttpStatusCode.OK);
        var bobsDelete = await Bob.DeleteAsync($"/api/watchlist/{movie.Id}", Ct);
        var bobsPut = await Bob.PutAsJsonAsync($"/api/watchlist/{movie.Id}", new { note = "Bob's note" }, Ct);

        Assert.Empty(bobsEmptyWatchlist.Items);
        await ReadProblemAsync(bobsDelete, HttpStatusCode.NotFound);
        // Created (not updated): Bob's watchlist did not contain the movie.
        Assert.Equal("Bob's note", (await ReadAsync<WatchlistItemResponse>(bobsPut, HttpStatusCode.Created)).Note);
        var alicesWatchlist = await GetAsync<PagedResponse<WatchlistItemResponse>>("/api/watchlist");
        Assert.Equal("Alice's note", Assert.Single(alicesWatchlist.Items).Note);
    }

    [Fact]
    public async Task A_movie_in_another_users_diary_cannot_be_deleted()
    {
        var movie = await CreateMovieAsync();
        await Bob.PostAsJsonAsync("/api/diary-entries", new { movieId = movie.Id, watchedOn = "2026-10-15" }, Ct);

        var response = await Client.DeleteAsync($"/api/movies/{movie.Id}", Ct);

        var problem = await ReadProblemAsync(response, HttpStatusCode.Conflict);
        Assert.DoesNotContain("bob", problem.Detail ?? "", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Deleting_a_movie_removes_it_from_all_watchlists()
    {
        var movie = await CreateMovieAsync();
        await Client.PutAsJsonAsync($"/api/watchlist/{movie.Id}", new { }, Ct);
        await Bob.PutAsJsonAsync($"/api/watchlist/{movie.Id}", new { }, Ct);

        Assert.Equal(HttpStatusCode.NoContent, (await Client.DeleteAsync($"/api/movies/{movie.Id}", Ct)).StatusCode);

        var bobsWatchlist = await ReadAsync<PagedResponse<WatchlistItemResponse>>(await Bob.GetAsync("/api/watchlist", Ct), HttpStatusCode.OK);
        Assert.Empty(bobsWatchlist.Items);
    }
}
