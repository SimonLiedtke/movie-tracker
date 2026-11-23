using System.Net;
using Microsoft.AspNetCore.Components.Forms;
using MovieTracker.Blazor.Tests.Support;

namespace MovieTracker.Blazor.Tests.Features;

public class LetterboxdImportTests : AppTest
{
    private static readonly Guid JobId = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private static readonly string JobPath = $"/api/imports/letterboxd/{JobId}";

    private static ImportJob Job(ImportStatus status, int total = 0, int processed = 0, ImportResult? result = null, string? error = null) =>
        new(JobId, status, total, processed, result, error);

    private async Task<IRenderedComponent<App>> OpenAsync(string url = "/import")
    {
        await SignInAsync();
        var app = Open(url);
        app.WaitForAssertion(() => Assert.Contains("Import from Letterboxd", app.Find("h1").TextContent));
        return app;
    }

    private static void Choose(IRenderedComponent<App> app, string name = "letterboxd-export.zip") =>
        app.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromBinary([80, 75, 3, 4], name, null, "application/zip"));

    [Fact]
    public async Task The_upload_form_explains_the_export_and_needs_a_file()
    {
        var app = await OpenAsync();

        Assert.Contains("Export your data", app.Find(".content").TextContent);
        Assert.Equal(".zip,application/zip", app.Find("input[type=file]").GetAttribute("accept"));
        Assert.Equal("No file chosen", app.Find(".file-name").TextContent);
        Assert.True(app.Find("button[type=submit]").HasAttribute("disabled"));
    }

    [Fact]
    public async Task Choosing_a_file_enables_starting_the_import()
    {
        var app = await OpenAsync();

        Choose(app);

        app.WaitForAssertion(() => Assert.Equal("letterboxd-export.zip", app.Find(".file-name").TextContent));
        Assert.False(app.Find("button[type=submit]").HasAttribute("disabled"));
    }

    [Fact]
    public async Task Starting_uploads_the_file_and_shows_the_job_in_the_URL()
    {
        Backend.Json(HttpMethod.Post, "/api/imports/letterboxd", Job(ImportStatus.Running), HttpStatusCode.Accepted);
        Backend.Get(JobPath, Job(ImportStatus.Running, 10, 4));
        var app = await OpenAsync();
        Choose(app);
        app.WaitForAssertion(() => Assert.False(app.Find("button[type=submit]").HasAttribute("disabled")));

        app.Find("form").Submit();

        app.WaitForAssertion(() => Assert.Equal($"/import?job={JobId}", CurrentUrl));
        var upload = Backend.Last(HttpMethod.Post, "/api/imports/letterboxd");
        Assert.Equal("multipart/form-data", upload.ContentType);
        Assert.Contains("name=file", upload.Body);
        Assert.Contains("filename=letterboxd-export.zip", upload.Body);
        app.WaitForAssertion(() => Assert.Contains("Looking up your films on TMDB: 4 of 10", app.Find(".box").Text()));
        var progress = app.Find("progress");
        Assert.Equal("4", progress.GetAttribute("value"));
        Assert.Equal("10", progress.GetAttribute("max"));
    }

    [Fact]
    public async Task A_refused_upload_shows_the_error_and_stays_on_the_form()
    {
        Backend.Problem(HttpMethod.Post, "/api/imports/letterboxd", HttpStatusCode.BadRequest, "This is not a Letterboxd export.");
        var app = await OpenAsync();
        Choose(app);
        app.WaitForAssertion(() => Assert.False(app.Find("button[type=submit]").HasAttribute("disabled")));

        app.Find("form").Submit();

        app.WaitForAssertion(() => Assert.Contains("This is not a Letterboxd export.", app.Find(".notification.is-danger").TextContent));
        Assert.Equal("/import", CurrentUrl);
        Assert.False(app.Find("button[type=submit]").HasAttribute("disabled"));
    }

    [Fact]
    public async Task A_job_that_has_not_started_counting_shows_an_indeterminate_progress_bar()
    {
        Backend.Get(JobPath, Job(ImportStatus.Running));

        var app = await OpenAsync($"/import?job={JobId}");

        app.WaitForAssertion(() => Assert.Contains("Starting the import…", app.Find(".box").Text()));
        Assert.False(app.Find("progress").HasAttribute("value"));
        Assert.False(app.Find("progress").HasAttribute("max"));
    }

    [Fact]
    public async Task A_running_job_is_asked_for_again_until_it_has_finished()
    {
        var asked = 0;
        Backend.On(HttpMethod.Get, JobPath, _ => FakeBackend.JsonResponse(
            ++asked < 2 ? Job(ImportStatus.Running, 10, 3) : Job(ImportStatus.Completed, 10, 10, new ImportResult(1, 0, 0, 0, [], [], []))));

        var app = await OpenAsync($"/import?job={JobId}");

        app.WaitForAssertion(() => Assert.Contains("The import is finished.", app.Markup), TimeSpan.FromSeconds(5));
        Assert.Equal(2, Backend.Sent(HttpMethod.Get, JobPath).Count());
    }

    [Fact]
    public async Task A_finished_import_shows_what_was_imported_and_skipped()
    {
        var result = new ImportResult(
            DiaryEntriesImported: 7, DiaryEntriesSkipped: 2, WatchlistItemsImported: 3, WatchlistItemsSkipped: 1,
            ManualMovies: [new ImportFilm("Obscure Film", 1971), new ImportFilm("No Year", null)],
            FailedFilms: [new ImportFilm("Failed Film", 2001)],
            Warnings: ["Row 4 had no date"]);
        Backend.Get(JobPath, Job(ImportStatus.Completed, 10, 10, result));

        var app = await OpenAsync($"/import?job={JobId}");

        app.WaitForAssertion(() => Assert.NotEmpty(app.FindAll(".notification.is-success")));
        var success = app.Find(".notification.is-success").Text();
        Assert.Contains("Diary entries imported: 7 (2 skipped, they were in your diary already)", success);
        Assert.Contains("Watchlist items imported: 3 (1 skipped, they were on your watchlist already)", success);
        Assert.Equal(["/diary", "/watchlist"], app.FindAll(".notification.is-success a").Select(a => a.GetAttribute("href")));
        Assert.Contains("TMDB did not answer for 1 films", app.Find(".notification.is-danger").Text());
        Assert.Contains("Failed Film (2001)", app.Find(".notification.is-danger").Text());
        var manual = app.Find(".notification.is-warning").Text();
        Assert.Contains("2 films could not be found on TMDB and were added as manual movies", manual);
        Assert.Contains("Obscure Film (1971)", manual);
        Assert.Contains("No Year", manual);
        Assert.DoesNotContain("No Year (", manual);
        Assert.Equal("/movies", app.Find(".notification.is-warning a").GetAttribute("href"));
        Assert.Contains("Row 4 had no date", app.Find("main").Text());
    }

    [Fact]
    public async Task Nothing_skipped_or_failed_shows_only_the_counts()
    {
        Backend.Get(JobPath, Job(ImportStatus.Completed, 2, 2, new ImportResult(2, 0, 1, 0, [], [], [])));

        var app = await OpenAsync($"/import?job={JobId}");

        app.WaitForAssertion(() => Assert.NotEmpty(app.FindAll(".notification.is-success")));
        Assert.DoesNotContain("skipped", app.Find(".notification.is-success").TextContent);
        Assert.Empty(app.FindAll(".notification.is-danger"));
        Assert.Empty(app.FindAll(".notification.is-warning"));
        Assert.Empty(app.FindAll("details"));
    }

    [Fact]
    public async Task A_finished_import_goes_back_to_the_form()
    {
        Backend.Get(JobPath, Job(ImportStatus.Completed, 1, 1, new ImportResult(1, 0, 0, 0, [], [], [])));
        var app = await OpenAsync($"/import?job={JobId}");
        app.WaitForAssertion(() => Assert.NotEmpty(app.FindAll(".notification.is-success")));

        app.Find("main button").Click();

        Assert.Equal("/import", CurrentUrl);
        app.WaitForAssertion(() => Assert.NotEmpty(app.FindAll("input[type=file]")));
        Assert.Equal("No file chosen", app.Find(".file-name").TextContent);
    }

    [Fact]
    public async Task A_failed_import_shows_why_and_offers_to_try_again()
    {
        Backend.Get(JobPath, Job(ImportStatus.Failed, error: "The archive has no diary.csv."));

        var app = await OpenAsync($"/import?job={JobId}");

        app.WaitForAssertion(() => Assert.Equal("The archive has no diary.csv.", app.Find(".notification.is-danger").Text()));
        app.Find("main button").Click();
        Assert.Equal("/import", CurrentUrl);
    }

    [Fact]
    public async Task An_import_the_server_does_not_know_any_more_asks_to_upload_again()
    {
        Backend.Problem(HttpMethod.Get, JobPath, HttpStatusCode.NotFound, "Unknown import");

        var app = await OpenAsync($"/import?job={JobId}");

        app.WaitForAssertion(() => Assert.Contains("This import is not available any more", app.Find(".notification.is-warning").Text()));
        Assert.NotEmpty(app.FindAll("form"));
        Assert.Empty(app.FindAll(".notification.is-danger"));
    }

    [Fact]
    public async Task A_job_id_that_is_none_is_not_available_either()
    {
        var app = await OpenAsync("/import?job=nonsense");

        Assert.Contains("This import is not available any more", app.Find(".notification.is-warning").Text());
    }

    [Fact]
    public async Task Another_error_while_asking_is_shown()
    {
        Backend.Problem(HttpMethod.Get, JobPath, HttpStatusCode.InternalServerError, "Database is down");

        var app = await OpenAsync($"/import?job={JobId}");

        app.WaitForAssertion(() => Assert.Contains("Database is down", app.Find(".notification.is-danger").TextContent));
    }
}
