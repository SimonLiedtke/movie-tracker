using System.Net;
using MovieTracker.Blazor.Tests.Support;

namespace MovieTracker.Blazor.Tests.Features;

public class ActivityLogTests : AppTest
{
    private static ActivityLogEntry Entry(int number, ActivityType type, Guid? movieId = null, string? title = "Movie 1", string? detail = null) =>
        new(Samples.Id(number), type, new DateTimeOffset(2026, 11, 7, 21, 30, 0, TimeSpan.Zero), movieId, title, detail);

    private async Task<IRenderedComponent<App>> OpenAsync(string url = "/activity")
    {
        await SignInAsync();
        var app = Open(url);
        app.WaitForAssertion(() => Assert.Contains("Activity log", app.Find("h1").TextContent));
        return app;
    }

    [Fact]
    public async Task Each_action_is_listed_with_time_label_movie_and_details()
    {
        Backend.Get("/api/activity-log", Samples.Page(
            [
                Entry(1, ActivityType.DiaryEntryAdded, Samples.Id(1), detail: "★★★"),
                Entry(2, ActivityType.MovieDeleted, null, "Gone"),
                Entry(3, ActivityType.LetterboxdImported, null, null, "12 viewings"),
            ], pageSize: 50));

        var app = await OpenAsync();

        app.WaitForAssertion(() => Assert.Equal(3, app.FindAll("tbody tr").Count));
        Assert.Contains("3 actions", app.Find("main").Text());
        var rows = app.FindAll("tbody tr");
        Assert.Equal("2026-11-07T21:30:00.0000000+00:00", rows[0].QuerySelector("time")!.GetAttribute("datetime"));
        Assert.Contains("Logged a viewing", rows[0].Text());
        Assert.Equal($"/movies/{Samples.Id(1)}", rows[0].QuerySelector("a")!.GetAttribute("href"));
        Assert.Equal("★★★", rows[0].QuerySelectorAll("td")[3].TextContent);
        // The movie was deleted since: its title stays, without a link.
        Assert.Contains("Deleted a movie", rows[1].Text());
        Assert.Null(rows[1].QuerySelector("a"));
        Assert.Contains("Gone", rows[1].Text());
        Assert.Contains("Imported from Letterboxd", rows[2].Text());
        Assert.Contains("12 viewings", rows[2].Text());
    }

    [Theory]
    [InlineData(ActivityType.DiaryEntryAdded, "Logged a viewing")]
    [InlineData(ActivityType.DiaryEntryUpdated, "Changed a viewing")]
    [InlineData(ActivityType.DiaryEntryDeleted, "Deleted a viewing")]
    [InlineData(ActivityType.WatchlistItemAdded, "Added to the watchlist")]
    [InlineData(ActivityType.WatchlistNoteChanged, "Changed a watchlist note")]
    [InlineData(ActivityType.WatchlistTagsChanged, "Changed watchlist tags")]
    [InlineData(ActivityType.WatchlistItemRemoved, "Removed from the watchlist")]
    [InlineData(ActivityType.MovieImported, "Imported a movie from TMDB")]
    [InlineData(ActivityType.MovieAdded, "Added a movie manually")]
    [InlineData(ActivityType.MovieUpdated, "Changed a movie")]
    [InlineData(ActivityType.MovieDeleted, "Deleted a movie")]
    [InlineData(ActivityType.LetterboxdImported, "Imported from Letterboxd")]
    public async Task Every_kind_of_action_has_a_label(ActivityType type, string label)
    {
        Backend.Get("/api/activity-log", Samples.Page([Entry(1, type)], pageSize: 50));

        var app = await OpenAsync();

        app.WaitForAssertion(() => Assert.Equal(label, app.FindAll("tbody td")[1].Text()));
    }

    [Fact]
    public async Task The_request_asks_for_the_page_in_the_URL()
    {
        Backend.Get("/api/activity-log", Samples.Page([Entry(1, ActivityType.MovieAdded)], page: 2, pageSize: 50, total: 120));

        var app = await OpenAsync("/activity?page=2");

        Assert.Equal("?page=2&pageSize=50", Backend.Last(HttpMethod.Get, "/api/activity-log").Query);
        app.WaitForAssertion(() => Assert.NotEmpty(app.FindAll(".pagination")));
        app.Find(".pagination-previous").Click();
        Assert.Equal("/activity", CurrentUrl);
    }

    [Fact]
    public async Task Paging_forward_puts_the_page_in_the_URL()
    {
        Backend.Get("/api/activity-log", Samples.Page([Entry(1, ActivityType.MovieAdded)], pageSize: 50, total: 120));
        var app = await OpenAsync();
        app.WaitForAssertion(() => Assert.NotEmpty(app.FindAll(".pagination")));

        app.Find(".pagination-next").Click();

        Assert.Equal("/activity?page=2", CurrentUrl);
    }

    [Fact]
    public async Task A_page_past_the_end_goes_to_the_last_page()
    {
        Backend.On(HttpMethod.Get, "/api/activity-log", request =>
            FakeBackend.JsonResponse(request.Query.Contains("page=9")
                ? new PagedResponse<ActivityLogEntry>([], 9, 50, 120)
                : Samples.Page([Entry(1, ActivityType.MovieAdded)], page: 3, pageSize: 50, total: 120)));
        await SignInAsync();

        Open("/activity?page=9");

        Assert.Equal("/activity?page=3", CurrentUrl);
    }

    [Fact]
    public async Task Nothing_to_show_says_so()
    {
        Backend.Get("/api/activity-log", Samples.Page<ActivityLogEntry>([], pageSize: 50));

        var app = await OpenAsync();

        app.WaitForAssertion(() => Assert.Equal("Nothing yet. Log a viewing or add a movie to your watchlist.", app.Find(".notification").Text()));
    }

    [Fact]
    public async Task A_failed_request_shows_the_error()
    {
        Backend.Problem(HttpMethod.Get, "/api/activity-log", HttpStatusCode.InternalServerError, "Database is down");

        var app = await OpenAsync();

        app.WaitForAssertion(() => Assert.Contains("Database is down", app.Find(".notification.is-danger").TextContent));
    }
}
