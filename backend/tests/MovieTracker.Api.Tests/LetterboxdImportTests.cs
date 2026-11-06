using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MovieTracker.Api.Contracts;
using MovieTracker.Api.Tests.Infrastructure;
using MovieTracker.Domain.Movies;
using MovieTracker.Infrastructure.Persistence;
using MovieTracker.Application.Tmdb;
using static MovieTracker.TestSupport.LetterboxdZip;

namespace MovieTracker.Api.Tests;

public sealed class LetterboxdImportTests(ApiFactory factory) : ApiTestBase(factory)
{
    private static readonly TmdbMovie Heat = new(
        949,
        new MovieDetails("Heat", "Heat", new DateOnly(1995, 12, 15), 170, null, "https://image.tmdb.org/t/p/w500/heat.jpg"),
        [28, 80]);

    [Fact]
    public async Task Imports_diary_and_watchlist()
    {
        Factory.Tmdb.Add(Heat);
        using var zip = Create(
            diary:
            [
                DiaryRow("Alien", 1979, watchedOn: "2026-09-30", rating: "4.5", rewatch: true, uri: "https://boxd.it/a"),
                DiaryRow("Aliens", 1986, watchedOn: "2026-10-05"),
            ],
            watchlist: [WatchlistRow("Heat", 1995, addedOn: "2026-08-15")],
            reviews: [ReviewRow("Alien", 1979, "https://boxd.it/a", "Scary,\r\nbut great.", watchedOn: "2026-09-30")],
            likedFilms: [LikedFilmRow("Alien", 1979)]);

        var job = await ImportAsync(zip);

        Assert.Equal(ImportStatus.Completed, job.Status);
        Assert.Equal(3, job.FilmsTotal);
        Assert.Equal(3, job.FilmsProcessed);
        var result = Assert.IsType<ImportResultResponse>(job.Result);
        Assert.Equal(2, result.DiaryEntriesImported);
        Assert.Equal(1, result.WatchlistItemsImported);
        Assert.Equal(0, result.DiaryEntriesSkipped + result.WatchlistItemsSkipped);
        Assert.Empty(result.ManualMovies);
        Assert.Empty(result.FailedFilms);

        var diary = await GetAsync<PagedResponse<DiaryEntryResponse>>("/api/diary-entries");
        Assert.Equal(["Aliens", "Alien"], diary.Items.Select(entry => entry.Movie.Title));
        var alien = diary.Items[1];
        Assert.Equal(new DateOnly(2026, 9, 30), alien.WatchedOn);
        Assert.Equal(9, alien.Rating);
        Assert.True(alien.IsRewatch);
        Assert.True(alien.Liked);
        Assert.Equal("Scary,\r\nbut great.", alien.Review);
        Assert.Null(diary.Items[0].Rating);
        Assert.False(diary.Items[0].Liked);

        var watchlist = await GetAsync<PagedResponse<WatchlistItemResponse>>("/api/watchlist");
        var heat = Assert.Single(watchlist.Items);
        Assert.Equal("Heat", heat.Movie.Title);
        Assert.Equal(new DateTimeOffset(2026, 8, 15, 0, 0, 0, TimeSpan.Zero), heat.AddedAt);

        // The movies are the TMDB ones, with their data and genres.
        var movie = await GetAsync<MovieResponse>($"/api/movies/{alien.Movie.Id}");
        Assert.Equal(FakeTmdbClient.Alien.TmdbId, movie.TmdbId);
        Assert.Equal(117, movie.RuntimeMinutes);
        Assert.Equal([Horror, ScienceFiction], movie.Genres.Select(genre => genre.Id).Order());
    }

    [Fact]
    public async Task Does_not_mistake_a_sequel_for_the_film()
    {
        using var zip = Create(diary: [DiaryRow("Alien", null)]);

        await ImportAsync(zip);

        var diary = await GetAsync<PagedResponse<DiaryEntryResponse>>("/api/diary-entries");
        Assert.Equal("Alien", Assert.Single(diary.Items).Movie.Title);
    }

    [Fact]
    public async Task Importing_the_same_export_again_adds_nothing_and_does_not_ask_TMDB_again()
    {
        using var first = Create(
            diary: [DiaryRow("Alien", 1979, watchedOn: "2026-09-30"), DiaryRow("Alien", 1979, watchedOn: "2026-10-30", rewatch: true)],
            watchlist: [WatchlistRow("Aliens", 1986)]);
        await ImportAsync(first);
        var findCalls = Factory.Tmdb.FindCalls;
        var getCalls = Factory.Tmdb.GetMovieCalls;

        using var second = Create(
            diary: [DiaryRow("Alien", 1979, watchedOn: "2026-09-30"), DiaryRow("Alien", 1979, watchedOn: "2026-10-30", rewatch: true)],
            watchlist: [WatchlistRow("Aliens", 1986)]);
        var job = await ImportAsync(second);

        var result = Assert.IsType<ImportResultResponse>(job.Result);
        Assert.Equal((0, 2, 0, 1), (result.DiaryEntriesImported, result.DiaryEntriesSkipped, result.WatchlistItemsImported, result.WatchlistItemsSkipped));
        Assert.Equal(findCalls, Factory.Tmdb.FindCalls);
        Assert.Equal(getCalls, Factory.Tmdb.GetMovieCalls);
        Assert.Equal(2, (await GetAsync<PagedResponse<DiaryEntryResponse>>("/api/diary-entries")).TotalCount);
        Assert.Equal(2, (await GetAsync<PagedResponse<MovieSummaryResponse>>("/api/movies")).TotalCount);
    }

    [Fact]
    public async Task Imports_a_movie_logged_twice_on_the_same_day_twice_but_only_once_more_next_time()
    {
        using var first = Create(diary: [DiaryRow("Alien", 1979, watchedOn: "2026-09-30"), DiaryRow("Alien", 1979, watchedOn: "2026-09-30")]);
        var job = await ImportAsync(first);
        Assert.Equal(2, job.Result!.DiaryEntriesImported);

        // The export grew by one viewing on the same day.
        using var second = Create(diary: Enumerable.Repeat(DiaryRow("Alien", 1979, watchedOn: "2026-09-30"), 3));
        job = await ImportAsync(second);

        Assert.Equal((1, 2), (job.Result!.DiaryEntriesImported, job.Result.DiaryEntriesSkipped));
    }

    [Fact]
    public async Task Skips_viewings_that_the_user_logged_by_hand()
    {
        var alien = await ImportTmdbMovieAsync(FakeTmdbClient.Alien.TmdbId);
        await CreateDiaryEntryAsync(alien.Id, watchedOn: "2026-09-30", rating: 3);
        using var zip = Create(diary: [DiaryRow("Alien", 1979, watchedOn: "2026-09-30", rating: "5")]);

        var job = await ImportAsync(zip);

        Assert.Equal((0, 1), (job.Result!.DiaryEntriesImported, job.Result.DiaryEntriesSkipped));
        var entry = Assert.Single((await GetAsync<PagedResponse<DiaryEntryResponse>>("/api/diary-entries")).Items);
        Assert.Equal(3, entry.Rating);
    }

    [Fact]
    public async Task Keeps_an_existing_watchlist_item_as_it_is()
    {
        var alien = await ImportTmdbMovieAsync(FakeTmdbClient.Alien.TmdbId);
        await Client.PutAsJsonAsync($"/api/watchlist/{alien.Id}", new { note = "With Bob" }, Ct);
        using var zip = Create(watchlist: [WatchlistRow("Alien", 1979, addedOn: "2020-01-01"), WatchlistRow("Alien", 1979, addedOn: "2020-02-02")]);

        var job = await ImportAsync(zip);

        Assert.Equal((0, 2), (job.Result!.WatchlistItemsImported, job.Result.WatchlistItemsSkipped));
        var item = Assert.Single((await GetAsync<PagedResponse<WatchlistItemResponse>>("/api/watchlist")).Items);
        Assert.Equal("With Bob", item.Note);
        Assert.Equal(ApiFactory.StartTime, item.AddedAt);
    }

    [Fact]
    public async Task Accepts_a_release_year_that_is_one_year_off()
    {
        // Letterboxd says 1980 (e.g. a later release), TMDB 1979.
        using var zip = Create(diary: [DiaryRow("Alien", 1980)]);

        var job = await ImportAsync(zip);

        Assert.Empty(job.Result!.ManualMovies);
        var movie = Assert.Single((await GetAsync<PagedResponse<MovieSummaryResponse>>("/api/movies")).Items);
        Assert.Equal(new DateOnly(1979, 5, 25), movie.ReleaseDate);
    }

    [Fact]
    public async Task Reuses_a_movie_that_another_user_imported_from_TMDB()
    {
        await ImportTmdbMovieAsync(FakeTmdbClient.Alien.TmdbId, Bob);
        var getCalls = Factory.Tmdb.GetMovieCalls;
        using var zip = Create(diary: [DiaryRow("Alien", 1979)]);

        await ImportAsync(zip);

        Assert.Equal(getCalls, Factory.Tmdb.GetMovieCalls);
        Assert.Equal(1, await CountMoviesInDatabaseAsync());
        Assert.Equal(1, (await GetAsync<PagedResponse<MovieSummaryResponse>>("/api/movies")).TotalCount);
    }

    [Fact]
    public async Task Finds_a_movie_again_that_was_saved_under_a_title_in_another_language()
    {
        // Tmdb:Language is de-DE: the movie is saved with its German title, but Letterboxd says "Alien" and the
        // search compares English titles. The movie that exists is used instead of fetching it a second time.
        Factory.Tmdb.Add(new TmdbMovie(
            348, new MovieDetails("Das unheimliche Wesen", null, new DateOnly(1979, 5, 25), 117, null, null), [Horror]));
        Factory.Tmdb.EnglishTitles[348] = "Alien";
        await ImportTmdbMovieAsync(348);
        var getCalls = Factory.Tmdb.GetMovieCalls;
        using var zip = Create(diary: [DiaryRow("Alien", 1979)]);

        var job = await ImportAsync(zip);

        Assert.Equal(1, job.Result!.DiaryEntriesImported);
        Assert.Equal(getCalls, Factory.Tmdb.GetMovieCalls);
        Assert.Equal(1, await CountMoviesInDatabaseAsync());
    }

    [Fact]
    public async Task Uses_the_users_manual_movie_with_the_same_title_and_year()
    {
        var manual = await CreateMovieAsync("Alien", releaseDate: "1979-05-25");
        using var zip = Create(diary: [DiaryRow("Alien", 1979)]);

        var job = await ImportAsync(zip);

        Assert.Equal(0, Factory.Tmdb.FindCalls);
        Assert.Equal(1, job.Result!.DiaryEntriesImported);
        Assert.Equal(manual.Id, Assert.Single((await GetAsync<PagedResponse<DiaryEntryResponse>>("/api/diary-entries")).Items).Movie.Id);
    }

    [Fact]
    public async Task Does_not_use_another_users_manual_movie()
    {
        await CreateMovieAsync("Alien", releaseDate: "1979-05-25", client: Bob);
        using var zip = Create(diary: [DiaryRow("Alien", 1979)]);

        await ImportAsync(zip);

        var movie = Assert.Single((await GetAsync<PagedResponse<MovieSummaryResponse>>("/api/movies")).Items);
        Assert.Equal(FakeTmdbClient.Alien.TmdbId, (await GetAsync<MovieResponse>($"/api/movies/{movie.Id}")).TmdbId);
    }

    [Fact]
    public async Task Adds_a_film_that_TMDB_does_not_know_as_a_manual_movie()
    {
        using var zip = Create(
            diary: [DiaryRow("Home Video", 2019, watchedOn: "2026-10-01")],
            watchlist: [WatchlistRow("Obscure Short", null)]);

        var job = await ImportAsync(zip);

        var result = job.Result!;
        Assert.Equal(1, result.DiaryEntriesImported);
        Assert.Equal(1, result.WatchlistItemsImported);
        Assert.Equal(
            [new ImportFilmResponse("Home Video", 2019), new ImportFilmResponse("Obscure Short", null)],
            result.ManualMovies.OrderBy(film => film.Name));

        var diary = await GetAsync<PagedResponse<DiaryEntryResponse>>("/api/diary-entries");
        var movie = await GetAsync<MovieResponse>($"/api/movies/{diary.Items[0].Movie.Id}");
        Assert.Null(movie.TmdbId);
        Assert.Equal("Home Video", movie.Title);
        Assert.Equal(new DateOnly(2019, 1, 1), movie.ReleaseDate);

        // The movie is private to the user and can be fixed by them.
        Assert.Equal(HttpStatusCode.NotFound, (await Bob.GetAsync($"/api/movies/{movie.Id}", Ct)).StatusCode);

        // Importing again finds the manual movie instead of adding another.
        using var again = Create(diary: [DiaryRow("Home Video", 2019, watchedOn: "2026-10-01")]);
        await ImportAsync(again);
        Assert.Equal(2, (await GetAsync<PagedResponse<MovieSummaryResponse>>("/api/movies")).TotalCount);
    }

    [Fact]
    public async Task Adds_all_films_as_manual_movies_if_TMDB_is_not_configured()
    {
        Factory.Tmdb.IsConfigured = false;
        using var zip = Create(diary: [DiaryRow("Alien", 1979)], watchlist: [WatchlistRow("Aliens", 1986)]);

        var job = await ImportAsync(zip);

        Assert.Equal(ImportStatus.Completed, job.Status);
        Assert.Equal(2, job.Result!.ManualMovies.Count);
        Assert.Equal(0, Factory.Tmdb.FindCalls);
        var movies = await GetAsync<PagedResponse<MovieSummaryResponse>>("/api/movies");
        Assert.Equal(2, movies.TotalCount);
    }

    [Fact]
    public async Task Leaves_out_films_for_which_TMDB_fails_and_imports_them_next_time()
    {
        Factory.Tmdb.Add(Heat);
        Factory.Tmdb.FailingTitles.Add("Heat");
        using var zip = Create(diary: [DiaryRow("Alien", 1979), DiaryRow("Heat", 1995)]);

        var job = await ImportAsync(zip);

        var result = job.Result!;
        Assert.Equal(ImportStatus.Completed, job.Status);
        Assert.Equal(1, result.DiaryEntriesImported);
        Assert.Equal([new ImportFilmResponse("Heat", 1995)], result.FailedFilms);
        // No manual movie that would stand in the way of the real one.
        Assert.Empty(result.ManualMovies);
        Assert.Equal(1, (await GetAsync<PagedResponse<MovieSummaryResponse>>("/api/movies")).TotalCount);

        Factory.Tmdb.FailingTitles.Clear();
        using var again = Create(diary: [DiaryRow("Alien", 1979), DiaryRow("Heat", 1995)]);
        job = await ImportAsync(again);

        Assert.Equal((1, 1), (job.Result!.DiaryEntriesImported, job.Result.DiaryEntriesSkipped));
        Assert.Empty(job.Result.FailedFilms);
    }

    [Fact]
    public async Task Imports_nothing_if_TMDB_is_down()
    {
        Factory.Tmdb.IsUnavailable = true;
        using var zip = Create(diary: [DiaryRow("Alien", 1979)], watchlist: [WatchlistRow("Aliens", 1986)]);

        var job = await ImportAsync(zip);

        Assert.Equal(ImportStatus.Completed, job.Status);
        Assert.Equal(0, job.Result!.DiaryEntriesImported + job.Result.WatchlistItemsImported);
        Assert.Equal(2, job.Result.FailedFilms.Count);
        Assert.Empty(job.Result.ManualMovies);
        Assert.Equal(0, await CountMoviesInDatabaseAsync());
    }

    [Fact]
    public async Task Reports_the_rows_of_the_export_that_were_skipped()
    {
        using var zip = Create(diary: [DiaryRow("Alien", 1979), DiaryRow("Aliens", 1986, watchedOn: "never", loggedOn: "never")]);

        var job = await ImportAsync(zip);

        Assert.Equal(1, job.Result!.DiaryEntriesImported);
        Assert.Equal(["diary.csv, row 2: \"Aliens\" skipped, it has no valid date."], job.Result.Warnings);
    }

    [Fact]
    public async Task Imports_many_films_at_once()
    {
        const int count = 60;
        var rows = new List<string>();
        for (var i = 0; i < count; i++)
        {
            Factory.Tmdb.Add(new TmdbMovie(
                10_000 + i,
                new MovieDetails($"Film {i}", null, new DateOnly(2000 + (i % 20), 1, 1), 90, null, null),
                [Drama]));
            rows.Add(DiaryRow($"Film {i}", 2000 + (i % 20), watchedOn: $"2026-01-{(i % 28) + 1:00}"));
        }

        using var zip = Create(diary: rows);
        var job = await ImportAsync(zip);

        Assert.Equal(count, job.FilmsTotal);
        Assert.Equal(count, job.FilmsProcessed);
        Assert.Equal(count, job.Result!.DiaryEntriesImported);
        Assert.Empty(job.Result.ManualMovies);
        Assert.Equal(count, await CountMoviesInDatabaseAsync());
    }

    [Fact]
    public async Task Each_user_gets_their_own_diary_from_the_same_export()
    {
        using var forAlice = Create(diary: [DiaryRow("Alien", 1979)]);
        using var forBob = Create(diary: [DiaryRow("Alien", 1979)]);

        await ImportAsync(forAlice);
        await ImportAsync(forBob, Bob);

        Assert.Equal(1, (await GetAsync<PagedResponse<DiaryEntryResponse>>("/api/diary-entries")).TotalCount);
        var bobsDiary = await Bob.GetFromJsonAsync<PagedResponse<DiaryEntryResponse>>("/api/diary-entries", Ct);
        Assert.Equal(1, bobsDiary!.TotalCount);
        // The TMDB movie is shared.
        Assert.Equal(1, await CountMoviesInDatabaseAsync());
    }

    [Fact]
    public async Task Answers_with_the_progress_while_the_import_runs_and_refuses_a_second_import()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Factory.Tmdb.FindGate = gate;
        using var zip = Create(diary: [DiaryRow("Alien", 1979)]);

        var response = await UploadAsync(zip);
        var started = await ReadAsync<ImportJobResponse>(response, HttpStatusCode.Accepted);

        Assert.Equal(ImportStatus.Running, started.Status);
        Assert.EndsWith($"/api/imports/letterboxd/{started.Id}", response.Headers.Location?.ToString());

        using var second = Create(diary: [DiaryRow("Aliens", 1986)]);
        var conflict = await ReadProblemAsync(await UploadAsync(second), HttpStatusCode.Conflict);
        Assert.Equal("Import already running", conflict.Title);

        // Another user can import at the same time (their import waits for the first one to finish).
        using var forBob = Create(diary: [DiaryRow("Alien", 1979)]);
        var bobsResponse = await UploadAsync(forBob, Bob);
        Assert.Equal(HttpStatusCode.Accepted, bobsResponse.StatusCode);

        var running = await GetAsync<ImportJobResponse>($"/api/imports/letterboxd/{started.Id}");
        Assert.Equal(ImportStatus.Running, running.Status);
        Assert.Null(running.Result);

        gate.SetResult();
        var finished = await WaitUntilFinishedAsync(started.Id);
        Assert.Equal(ImportStatus.Completed, finished.Status);
        await WaitUntilFinishedAsync((await ReadAsync<ImportJobResponse>(bobsResponse, HttpStatusCode.Accepted)).Id, Bob);

        // Finished: the next import is allowed.
        using var third = Create(diary: [DiaryRow("Aliens", 1986)]);
        Assert.Equal(ImportStatus.Completed, (await ImportAsync(third)).Status);
    }

    [Fact]
    public async Task Other_users_do_not_see_an_import()
    {
        using var zip = Create(diary: [DiaryRow("Alien", 1979)]);
        var job = await ImportAsync(zip);

        var response = await Bob.GetAsync($"/api/imports/letterboxd/{job.Id}", Ct);

        await ReadProblemAsync(response, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Returns_404_for_an_unknown_import()
    {
        var response = await Client.GetAsync($"/api/imports/letterboxd/{Guid.NewGuid()}", Ct);

        await ReadProblemAsync(response, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Requires_a_signed_in_user()
    {
        using var zip = Create(diary: [DiaryRow("Alien", 1979)]);

        Assert.Equal(HttpStatusCode.Unauthorized, (await UploadAsync(zip, Anonymous)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Anonymous.GetAsync($"/api/imports/letterboxd/{Guid.NewGuid()}", Ct)).StatusCode);
    }

    [Fact]
    public async Task Rejects_a_file_that_is_not_a_zip_archive()
    {
        using var content = new MultipartFormDataContent { { new StringContent("Date,Name\n"), "file", "diary.csv" } };

        var response = await Client.PostAsync("/api/imports/letterboxd", content, Ct);

        var problem = await ReadValidationProblemAsync(response);
        Assert.Contains("ZIP", Assert.Single(problem.Errors["file"]));
    }

    [Fact]
    public async Task Rejects_an_archive_that_is_not_a_Letterboxd_export()
    {
        using var zip = CreateFromFiles(("readme.txt", "hello"));

        var problem = await ReadValidationProblemAsync(await UploadAsync(zip));

        Assert.Contains("diary.csv", Assert.Single(problem.Errors["file"]));
    }

    [Fact]
    public async Task Rejects_a_request_without_a_file()
    {
        using var content = new MultipartFormDataContent { { new StringContent("x"), "other" } };

        var response = await Client.PostAsync("/api/imports/letterboxd", content, Ct);

        var problem = await ReadValidationProblemAsync(response);
        Assert.Contains("file", problem.Errors.Keys);
    }

    private Task<HttpResponseMessage> UploadAsync(Stream zip, HttpClient? client = null)
    {
        var content = new MultipartFormDataContent { { new StreamContent(zip), "file", "letterboxd-export.zip" } };
        return (client ?? Client).PostAsync("/api/imports/letterboxd", content, Ct);
    }

    /// <summary>Uploads the export and waits until the import has finished.</summary>
    private async Task<ImportJobResponse> ImportAsync(Stream zip, HttpClient? client = null)
    {
        var started = await ReadAsync<ImportJobResponse>(await UploadAsync(zip, client), HttpStatusCode.Accepted);
        return await WaitUntilFinishedAsync(started.Id, client);
    }

    private async Task<ImportJobResponse> WaitUntilFinishedAsync(Guid importId, HttpClient? client = null)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));

        while (true)
        {
            var response = await (client ?? Client).GetAsync($"/api/imports/letterboxd/{importId}", timeout.Token);
            var job = await ReadAsync<ImportJobResponse>(response, HttpStatusCode.OK);
            if (job.Status != ImportStatus.Running)
            {
                return job;
            }

            await Task.Delay(20, timeout.Token);
        }
    }

    private async Task<int> CountMoviesInDatabaseAsync()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MovieTrackerDbContext>();
        return await db.Movies.CountAsync(Ct);
    }
}
