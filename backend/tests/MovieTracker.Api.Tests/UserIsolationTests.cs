using System.Net;
using System.Net.Http.Json;
using MovieTracker.Api.Contracts;
using MovieTracker.Api.Tests.Infrastructure;

namespace MovieTracker.Api.Tests;

/// <summary>Users share movies from TMDB, but not their manually added movies, diaries and watchlists.</summary>
public sealed class UserIsolationTests(ApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task Other_users_cannot_read_change_or_delete_a_diary_entry()
    {
        var movie = await ImportTmdbMovieAsync();
        var entry = await CreateDiaryEntryAsync(movie.Id, review: "Alice's review");
        var url = $"/api/diary-entries/{entry.Id}";

        var get = await Bob.GetAsync(url, Ct);
        var put = await Bob.PutAsJsonAsync(url, new { movieId = movie.Id, watchedOn = "2026-10-01", review = "Bob was here" }, Ct);
        var delete = await Bob.DeleteAsync(url, Ct);

        // 404, not 403: other users' entries do not even exist for Bob.
        await ReadProblemAsync(get, HttpStatusCode.NotFound);
        await ReadProblemAsync(put, HttpStatusCode.NotFound);
        await ReadProblemAsync(delete, HttpStatusCode.NotFound);
        Assert.Equivalent(entry, await GetAsync<DiaryEntryResponse>(url));
    }

    [Fact]
    public async Task Diaries_only_list_the_users_own_entries()
    {
        var movie = await ImportTmdbMovieAsync();
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
        Assert.Equivalent(new[] { bobsEntry }, bobsDiary.Items, strict: true);
        Assert.Equivalent(new[] { bobsEntry }, bobsMovieEntries, strict: true);
    }

    [Fact]
    public async Task Movie_statistics_are_those_of_the_signed_in_user()
    {
        var movie = await ImportTmdbMovieAsync();
        await CreateDiaryEntryAsync(movie.Id, rating: 10);
        await Client.PutAsJsonAsync($"/api/watchlist/{movie.Id}", new { }, Ct);

        var detail = await ReadAsync<MovieResponse>(await Bob.GetAsync($"/api/movies/{movie.Id}", Ct), HttpStatusCode.OK);
        var list = await ReadAsync<PagedResponse<MovieSummaryResponse>>(await Bob.GetAsync("/api/movies", Ct), HttpStatusCode.OK);

        Assert.Equal((null, 0, false, null), (detail.LatestRating, detail.WatchCount, detail.OnWatchlist, detail.LastWatchedOn));
        // Not one of Bob's movies: he has not logged or watchlisted it.
        Assert.Empty(list.Items);
        var own = await GetAsync<MovieResponse>($"/api/movies/{movie.Id}");
        Assert.Equal((10, 1, true), (own.LatestRating, own.WatchCount, own.OnWatchlist));
    }

    [Fact]
    public async Task Every_user_has_their_own_watchlist()
    {
        var movie = await ImportTmdbMovieAsync();
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
    public async Task Other_users_cannot_see_change_or_delete_a_manually_added_movie()
    {
        var movie = await CreateMovieAsync("Home Video");
        var url = $"/api/movies/{movie.Id}";

        var get = await Bob.GetAsync(url, Ct);
        var entries = await Bob.GetAsync($"{url}/diary-entries", Ct);
        var put = await Bob.PutAsJsonAsync(url, new { title = "Bob was here", genreIds = Array.Empty<int>() }, Ct);
        var delete = await Bob.DeleteAsync(url, Ct);
        var bobsList = await ReadAsync<PagedResponse<MovieSummaryResponse>>(await Bob.GetAsync("/api/movies", Ct), HttpStatusCode.OK);

        await ReadProblemAsync(get, HttpStatusCode.NotFound);
        await ReadProblemAsync(entries, HttpStatusCode.NotFound);
        await ReadProblemAsync(put, HttpStatusCode.NotFound);
        await ReadProblemAsync(delete, HttpStatusCode.NotFound);
        Assert.Empty(bobsList.Items);
        Assert.Equal("Home Video", (await GetAsync<MovieResponse>(url)).Title);
    }

    [Fact]
    public async Task Other_users_cannot_log_or_watchlist_a_manually_added_movie()
    {
        var movie = await CreateMovieAsync("Home Video");
        var bobsMovie = await ImportTmdbMovieAsync(client: Bob);
        var bobsEntry = await ReadAsync<DiaryEntryResponse>(
            await Bob.PostAsJsonAsync("/api/diary-entries", new { movieId = bobsMovie.Id, watchedOn = "2026-10-15" }, Ct),
            HttpStatusCode.Created);

        var create = await Bob.PostAsJsonAsync("/api/diary-entries", new { movieId = movie.Id, watchedOn = "2026-10-15" }, Ct);
        var update = await Bob.PutAsJsonAsync($"/api/diary-entries/{bobsEntry.Id}", new { movieId = movie.Id, watchedOn = "2026-10-15" }, Ct);
        var watchlist = await Bob.PutAsync($"/api/watchlist/{movie.Id}", content: null, Ct);

        // Same answers as for a movie that does not exist.
        await ReadProblemAsync(create, HttpStatusCode.UnprocessableEntity);
        await ReadProblemAsync(update, HttpStatusCode.UnprocessableEntity);
        await ReadProblemAsync(watchlist, HttpStatusCode.NotFound);
    }
}
