using System.Net;
using System.Net.Http.Json;
using MovieTracker.Api.Contracts;
using MovieTracker.Api.Tests.Infrastructure;

namespace MovieTracker.Api.Tests;

/// <summary>Searching movies (own manual movies and TMDB) and importing them from TMDB.</summary>
public sealed class TmdbTests(ApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task Search_lists_the_users_manual_movies_first_then_tmdb_results()
    {
        var manual = await CreateMovieAsync("Alien Home Video", releaseDate: null);

        var result = await GetAsync<MovieSearchResponse>("/api/movies/search?query=alien");

        Assert.True(result.TmdbAvailable);
        Assert.Equal(
            [
                new MovieSearchResult(manual.Id, null, "Alien Home Video", null, null, null),
                new MovieSearchResult(null, 348, "Alien", "Alien", new DateOnly(1979, 5, 25), "https://image.tmdb.org/t/p/w500/alien.jpg"),
                new MovieSearchResult(null, 679, "Aliens", "Aliens", new DateOnly(1986, 7, 18), null),
            ],
            result.Items);
    }

    [Fact]
    public async Task Search_includes_the_movie_id_of_tmdb_movies_that_were_imported_before()
    {
        var imported = await ImportTmdbMovieAsync(FakeTmdbClient.Aliens.TmdbId, Bob);

        var result = await GetAsync<MovieSearchResponse>("/api/movies/search?query=aliens");

        Assert.Equal((imported.Id, 679), (Assert.Single(result.Items).MovieId, result.Items[0].TmdbId));
    }

    [Fact]
    public async Task Search_does_not_find_other_users_manual_movies()
    {
        await CreateMovieAsync("Alien Home Video", client: Bob);

        var result = await GetAsync<MovieSearchResponse>("/api/movies/search?query=home");

        Assert.Empty(result.Items);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task Search_falls_back_to_manual_movies_if_tmdb_is_not_usable(bool configured, bool unavailable)
    {
        Factory.Tmdb.IsConfigured = configured;
        Factory.Tmdb.IsUnavailable = unavailable;
        var manual = await CreateMovieAsync("Alien Home Video");

        var result = await GetAsync<MovieSearchResponse>("/api/movies/search?query=alien");

        Assert.False(result.TmdbAvailable);
        Assert.Equal([manual.Id], result.Items.Select(item => item.MovieId));
    }

    [Theory]
    [InlineData("")]
    [InlineData("?query=")]
    public async Task Search_requires_a_query(string queryString)
    {
        var response = await Client.GetAsync($"/api/movies/search{queryString}", Ct);

        await ReadValidationProblemAsync(response);
    }

    [Fact]
    public async Task Import_creates_the_movie_from_tmdb()
    {
        var response = await Client.PostAsync("/api/movies/tmdb/348", content: null, Ct);

        var movie = await ReadAsync<MovieResponse>(response, HttpStatusCode.Created);
        Assert.Equal($"/api/movies/{movie.Id}", response.Headers.Location?.AbsolutePath);
        Assert.Equal(348, movie.TmdbId);
        Assert.Equal("Alien", movie.Title);
        Assert.Equal(new DateOnly(1979, 5, 25), movie.ReleaseDate);
        Assert.Equal(117, movie.RuntimeMinutes);
        Assert.Equal("In space no one can hear you scream.", movie.Overview);
        Assert.Equal("https://image.tmdb.org/t/p/w500/alien.jpg", movie.PosterUrl);
        Assert.Equal(["Horror", "Science Fiction"], movie.Genres.Select(genre => genre.Name));
        Assert.Equal(ApiFactory.StartTime, movie.CreatedAt);
    }

    [Fact]
    public async Task Import_skips_genres_that_are_not_seeded()
    {
        var movie = await ImportTmdbMovieAsync(FakeTmdbClient.Aliens.TmdbId);

        Assert.Equal(["Action", "Science Fiction"], movie.Genres.Select(genre => genre.Name));
    }

    [Fact]
    public async Task Import_returns_the_existing_movie_if_it_was_imported_before()
    {
        var first = await ImportTmdbMovieAsync(client: Bob);
        await CreateDiaryEntryAsync(first.Id, rating: 7);

        var response = await Client.PostAsync("/api/movies/tmdb/348", content: null, Ct);

        // Shared by all users, but with Alice's statistics.
        var second = await ReadAsync<MovieResponse>(response, HttpStatusCode.OK);
        Assert.Equal(first.Id, second.Id);
        Assert.Equal((7, 1), (second.LatestRating, second.WatchCount));
        Assert.Equal(1, Factory.Tmdb.GetMovieCalls);
    }

    [Fact]
    public async Task Import_returns_404_for_a_movie_tmdb_does_not_know()
    {
        var response = await Client.PostAsync("/api/movies/tmdb/999999", content: null, Ct);

        Assert.Equal("Unknown TMDB movie", (await ReadProblemAsync(response, HttpStatusCode.NotFound)).Title);
    }

    [Fact]
    public async Task Import_returns_502_if_tmdb_is_unavailable()
    {
        Factory.Tmdb.IsUnavailable = true;

        var response = await Client.PostAsync("/api/movies/tmdb/348", content: null, Ct);

        await ReadProblemAsync(response, HttpStatusCode.BadGateway);
    }

    [Fact]
    public async Task Import_returns_503_if_tmdb_is_not_configured()
    {
        Factory.Tmdb.IsConfigured = false;

        var response = await Client.PostAsync("/api/movies/tmdb/348", content: null, Ct);

        await ReadProblemAsync(response, HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task Imported_movies_can_be_logged_and_watchlisted_by_every_user()
    {
        var movie = await ImportTmdbMovieAsync();

        await CreateDiaryEntryAsync(movie.Id, rating: 10);
        var bobsEntry = await Bob.PostAsJsonAsync("/api/diary-entries", new { movieId = movie.Id, watchedOn = "2026-10-01", rating = 4 }, Ct);
        var bobsWatchlist = await Bob.PutAsync($"/api/watchlist/{movie.Id}", content: null, Ct);

        await ReadAsync<DiaryEntryResponse>(bobsEntry, HttpStatusCode.Created);
        await ReadAsync<WatchlistItemResponse>(bobsWatchlist, HttpStatusCode.Created);
        var alicesView = await GetAsync<MovieResponse>($"/api/movies/{movie.Id}");
        var bobsView = await ReadAsync<MovieResponse>(await Bob.GetAsync($"/api/movies/{movie.Id}", Ct), HttpStatusCode.OK);
        Assert.Equal((10, false), (alicesView.LatestRating, alicesView.OnWatchlist));
        Assert.Equal((4, true), (bobsView.LatestRating, bobsView.OnWatchlist));
    }
}
