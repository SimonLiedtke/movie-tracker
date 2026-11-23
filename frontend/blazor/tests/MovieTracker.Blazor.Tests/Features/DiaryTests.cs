using System.Net;
using MovieTracker.Blazor.Tests.Support;

namespace MovieTracker.Blazor.Tests.Features;

public class DiaryListTests : AppTest
{
    private static readonly DiaryEntry[] Entries =
    [
        Samples.Entry(1, movie: 1, watchedOn: new DateOnly(2026, 11, 7), rating: 8, review: "Great", isRewatch: true, liked: true, tags: ["cinema"]),
        Samples.Entry(2, movie: 2, watchedOn: new DateOnly(2026, 11, 2)),
        Samples.Entry(3, movie: 3, watchedOn: new DateOnly(2026, 10, 30), rating: 1),
    ];

    private async Task<IRenderedComponent<App>> OpenAsync(string url = "/diary", PagedResponse<DiaryEntry>? page = null)
    {
        Backend.Get("/api/diary-entries", page ?? Samples.Page(Entries));
        await SignInAsync();
        var app = Open(url);
        app.WaitForAssertion(() => Assert.NotEmpty(app.FindAll("main .box")));
        return app;
    }

    [Fact]
    public async Task Entries_are_grouped_by_month_in_the_order_of_the_API()
    {
        var app = await OpenAsync();

        app.WaitForAssertion(() => Assert.Equal(2, app.FindAll("tbody").Count));
        Assert.Equal(["November 2026", "October 2026"], app.FindAll("tbody th").Select(th => th.TextContent));
        Assert.Equal(["Sat 7", "Mon 2", "Fri 30"], app.FindAll("tbody td:first-child").Select(td => td.TextContent));
        Assert.Contains("3 entries", app.Find("main").TextContent);
    }

    [Fact]
    public async Task An_entry_shows_its_movie_rating_review_and_marks()
    {
        var app = await OpenAsync();

        app.WaitForAssertion(() => Assert.NotEmpty(app.FindAll("tbody tr")));
        var row = app.FindAll("tbody tr")[1];
        Assert.Equal($"/movies/{Samples.Id(1)}", row.QuerySelector("td a")!.GetAttribute("href"));
        Assert.Contains("Movie 1 (1981)", row.QuerySelectorAll("td")[1].TextContent);
        Assert.Contains("↻", row.TextContent);
        Assert.Contains("♥", row.TextContent);
        Assert.Contains("★★★★", row.TextContent);
        Assert.Contains("Great", row.TextContent);
        Assert.Equal("/diary?tag=cinema", row.QuerySelector("a.tag")!.GetAttribute("href"));
        Assert.Equal($"/diary/{Samples.Id(1001)}/edit", row.QuerySelector("a.button")!.GetAttribute("href"));
    }

    [Fact]
    public async Task The_request_carries_the_filters_of_the_URL()
    {
        var app = await OpenAsync("/diary?from=2026-01-01&to=2026-12-31&tag=%20cinema&ratingFrom=3&ratingTo=9&page=2");

        Assert.Equal(
            "?from=2026-01-01&to=2026-12-31&tag=cinema&ratingFrom=3&ratingTo=9&page=2&pageSize=25",
            Backend.Last(HttpMethod.Get, "/api/diary-entries").Query);
        Assert.Equal("2026-01-01", app.Find("#from").GetAttribute("value"));
        Assert.Equal("2026-12-31", app.Find("#to").GetAttribute("value"));
        Assert.Equal("2026-12-31", app.Find("#from").GetAttribute("max"));
        Assert.Equal("2026-01-01", app.Find("#to").GetAttribute("min"));
        Assert.Equal("cinema", app.Find("#tag").GetAttribute("value"));
        Assert.Equal("3", app.Find("#ratingFrom").GetAttribute("value"));
        Assert.Equal("9", app.Find("#ratingTo").GetAttribute("value"));
    }

    [Fact]
    public async Task The_rating_range_cannot_be_reversed()
    {
        var app = await OpenAsync("/diary?ratingFrom=4&ratingTo=8");

        var minDisabled = app.FindAll("#ratingFrom option").Where(o => o.HasAttribute("disabled")).Select(o => o.GetAttribute("value"));
        var maxDisabled = app.FindAll("#ratingTo option").Where(o => o.HasAttribute("disabled")).Select(o => o.GetAttribute("value"));
        Assert.Equal(["9", "10"], minDisabled);
        Assert.Equal(["1", "2", "3"], maxDisabled);
    }

    [Fact]
    public async Task Changing_a_filter_goes_back_to_the_first_page_and_keeps_the_other_filters()
    {
        var app = await OpenAsync("/diary?tag=cinema&page=2");

        app.Find("#from").Change("2026-05-01");
        Assert.Equal("/diary?tag=cinema&from=2026-05-01", CurrentUrl);

        app.Find("#ratingFrom").Change("5");
        Assert.Equal("/diary?tag=cinema&from=2026-05-01&ratingFrom=5", CurrentUrl);

        app.Find("#tag").Change("  date night ");
        Assert.Equal("/diary?from=2026-05-01&ratingFrom=5&tag=date%20night", CurrentUrl);

        app.Find("#from").Change("");
        Assert.Equal("/diary?ratingFrom=5&tag=date%20night", CurrentUrl);
    }

    [Fact]
    public async Task The_filters_can_be_cleared_at_once()
    {
        var app = await OpenAsync("/diary?tag=cinema&from=2026-05-01");

        app.Find(".box button").Click();

        Assert.Equal("/diary", CurrentUrl);
        Assert.Empty(app.FindAll(".box button"));
    }

    [Fact]
    public async Task Without_a_filter_there_is_nothing_to_clear()
    {
        var app = await OpenAsync();

        Assert.Empty(app.FindAll(".box button"));
    }

    [Theory]
    [InlineData("/diary", "No diary entries yet. Log your first viewing.")]
    [InlineData("/diary?tag=x", "No diary entries for this filter.")]
    [InlineData("/diary?ratingFrom=3", "No diary entries for this filter.")]
    [InlineData("/diary?from=2026-01-01", "No diary entries in this period.")]
    public async Task An_empty_diary_says_why(string url, string expected)
    {
        var app = await OpenAsync(url, Samples.Page<DiaryEntry>([]));

        app.WaitForAssertion(() => Assert.Equal(expected, app.Find(".notification").Text()));
    }

    [Fact]
    public async Task Paging_goes_to_the_page_in_the_URL()
    {
        var app = await OpenAsync(page: Samples.Page(Entries, pageSize: 25, total: 80));

        app.WaitForAssertion(() => Assert.NotEmpty(app.FindAll(".pagination")));
        app.Find(".pagination-next").Click();

        Assert.Equal("/diary?page=2", CurrentUrl);
    }

    [Fact]
    public async Task A_page_past_the_end_goes_to_the_last_page()
    {
        Backend.On(HttpMethod.Get, "/api/diary-entries", request =>
            FakeBackend.JsonResponse(request.Query.Contains("page=9")
                ? new PagedResponse<DiaryEntry>([], 9, 25, 60)
                : Samples.Page(Entries, page: 3, pageSize: 25, total: 60)));
        await SignInAsync();

        Open("/diary?page=9");

        Assert.Equal("/diary?page=3", CurrentUrl);
    }

    [Fact]
    public async Task An_entry_is_deleted_after_confirming_and_the_list_is_loaded_again()
    {
        AnswerConfirm(true);
        Backend.NoContent(HttpMethod.Delete, $"/api/diary-entries/{Samples.Id(1001)}");
        var app = await OpenAsync();
        app.WaitForAssertion(() => Assert.NotEmpty(app.FindAll("tbody tr")));

        app.FindAll("tbody tr")[1].QuerySelector("button")!.Click();

        app.WaitForAssertion(() => Assert.Equal(2, Backend.Sent(HttpMethod.Get, "/api/diary-entries").Count()));
        Assert.Single(Backend.Sent(HttpMethod.Delete, $"/api/diary-entries/{Samples.Id(1001)}"));
        Assert.Equal(
            "Delete the diary entry for \"Movie 1\" from November 7, 2026?",
            JSInterop.Invocations["confirm"].Single().Arguments[0]);
    }

    [Fact]
    public async Task An_entry_is_kept_when_the_user_does_not_confirm()
    {
        AnswerConfirm(false);
        var app = await OpenAsync();
        app.WaitForAssertion(() => Assert.NotEmpty(app.FindAll("tbody tr")));

        app.FindAll("tbody tr")[1].QuerySelector("button")!.Click();

        app.WaitForAssertion(() => Assert.NotEmpty(JSInterop.Invocations["confirm"]));
        Assert.Empty(Backend.Sent(HttpMethod.Delete, $"/api/diary-entries/{Samples.Id(1001)}"));
    }

    [Fact]
    public async Task A_failed_delete_shows_the_error()
    {
        AnswerConfirm(true);
        Backend.Problem(HttpMethod.Delete, $"/api/diary-entries/{Samples.Id(1001)}", HttpStatusCode.InternalServerError, "Cannot delete");
        var app = await OpenAsync();
        app.WaitForAssertion(() => Assert.NotEmpty(app.FindAll("tbody tr")));

        app.FindAll("tbody tr")[1].QuerySelector("button")!.Click();

        app.WaitForAssertion(() => Assert.Contains("Cannot delete", app.Find(".notification.is-danger").TextContent));
    }
}

public class DiaryEntryFormTests : AppTest
{
    private static readonly Guid Entry = Samples.Id(1001);

    public DiaryEntryFormTests()
    {
        Backend.Json(HttpMethod.Post, "/api/diary-entries", Samples.Entry(1, movie: 2), HttpStatusCode.Created);
        Backend.Get($"/api/movies/{Samples.Id(2)}", Samples.Movie(2));
        Backend.Get($"/api/movies/{Samples.Id(2)}/diary-entries", Array.Empty<DiaryEntry>());
    }

    private async Task<IRenderedComponent<App>> OpenAsync(string url)
    {
        await SignInAsync();
        var app = Open(url);
        app.WaitForAssertion(() => Assert.NotEmpty(app.FindAll("form")));
        return app;
    }

    private static void Submit(IRenderedComponent<App> app) => app.Find("form").Submit();

    [Fact]
    public async Task The_date_starts_as_today_and_cannot_be_in_the_future()
    {
        var app = await OpenAsync("/diary/new");

        var today = Dates.Iso(Dates.Today());
        Assert.Equal(today, app.Find("#watchedOn").GetAttribute("value"));
        Assert.Equal(today, app.Find("#watchedOn").GetAttribute("max"));
        Assert.Contains("Log a viewing", app.Find("h1").TextContent);
        Assert.Contains("Tags the movie has on your watchlist are added automatically.", app.Find("#tags").ParentElement!.ParentElement!.TextContent);
    }

    [Fact]
    public async Task A_preselected_movie_is_shown_and_a_watched_one_is_a_rewatch()
    {
        Backend.Get($"/api/movies/{Samples.Id(2)}", Samples.Movie(2, watchCount: 3));

        var app = await OpenAsync($"/diary/new?movieId={Samples.Id(2)}");

        app.WaitForAssertion(() => Assert.Equal("Movie 2 (1982)", app.Find("#movie").GetAttribute("value")));
        Assert.True(app.Find("input[type=checkbox]").HasAttribute("checked"));
        Assert.Equal($"/movies/{Samples.Id(2)}", app.Find("a.is-light").GetAttribute("href"));
    }

    [Fact]
    public async Task A_preselected_movie_that_was_not_watched_is_no_rewatch()
    {
        var app = await OpenAsync($"/diary/new?movieId={Samples.Id(2)}");

        app.WaitForAssertion(() => Assert.Equal("Movie 2 (1982)", app.Find("#movie").GetAttribute("value")));
        Assert.False(app.Find("input[type=checkbox]").HasAttribute("checked"));
    }

    [Fact]
    public async Task Without_a_movie_saving_asks_for_one()
    {
        var app = await OpenAsync("/diary/new");

        Submit(app);

        Assert.Equal("Please pick a movie.", app.Find(".help.is-danger").TextContent);
        Assert.Empty(Backend.Sent(HttpMethod.Post, "/api/diary-entries"));
    }

    [Fact]
    public async Task Problems_of_the_fields_are_shown_when_they_are_touched()
    {
        var app = await OpenAsync($"/diary/new?movieId={Samples.Id(2)}");
        Assert.Empty(app.FindAll(".help.is-danger"));

        app.Find("#tags").Input(new string('x', 51));
        app.Find("#review").Input(new string('y', 20001));
        app.Find("#watchedOn").Change("");

        Assert.Equal(
            ["Please enter the date you watched the movie.", "At most 20,000 characters.", "A tag can have at most 50 characters."],
            app.FindAll(".help.is-danger").Select(h => h.TextContent));
    }

    [Fact]
    public async Task A_viewing_is_logged_with_what_was_entered()
    {
        var app = await OpenAsync($"/diary/new?movieId={Samples.Id(2)}");
        app.WaitForAssertion(() => Assert.Equal("Movie 2 (1982)", app.Find("#movie").GetAttribute("value")));
        app.Find("#watchedOn").Change("2026-11-02");
        app.Find("#rating").Change("7");
        app.Find("#review").Input("  Loved it  ");
        app.Find("#tags").Input("cinema, date night,");
        app.FindAll("input[type=checkbox]")[0].Change(true);
        app.FindAll("input[type=checkbox]")[1].Change(true);

        Submit(app);

        app.WaitForAssertion(() => Assert.Equal($"/movies/{Samples.Id(2)}", CurrentUrl));
        Assert.Equal(
            $$"""{"movieId":"{{Samples.Id(2)}}","watchedOn":"2026-11-02","rating":7,"review":"Loved it","isRewatch":true,"liked":true,"tags":["cinema","date night"]}""",
            Backend.Last(HttpMethod.Post, "/api/diary-entries").Body);
    }

    [Fact]
    public async Task Without_rating_and_review_they_are_sent_as_null()
    {
        var app = await OpenAsync($"/diary/new?movieId={Samples.Id(2)}");
        app.WaitForAssertion(() => Assert.Equal("Movie 2 (1982)", app.Find("#movie").GetAttribute("value")));
        app.Find("#watchedOn").Change("2026-11-02");

        Submit(app);

        app.WaitForAssertion(() => Assert.Single(Backend.Sent(HttpMethod.Post, "/api/diary-entries")));
        Assert.Equal(
            $$"""{"movieId":"{{Samples.Id(2)}}","watchedOn":"2026-11-02","rating":null,"review":null,"isRewatch":false,"liked":false,"tags":[]}""",
            Backend.Last(HttpMethod.Post, "/api/diary-entries").Body);
    }

    [Fact]
    public async Task A_refused_viewing_shows_the_servers_messages_and_stays()
    {
        Backend.Problem(HttpMethod.Post, "/api/diary-entries", HttpStatusCode.BadRequest, "Validation failed",
            new() { ["tags"] = ["Too many tags."] });
        var app = await OpenAsync($"/diary/new?movieId={Samples.Id(2)}");
        app.WaitForAssertion(() => Assert.Equal("Movie 2 (1982)", app.Find("#movie").GetAttribute("value")));

        Submit(app);

        app.WaitForAssertion(() => Assert.Contains("Too many tags.", app.Find(".notification.is-danger").TextContent));
        Assert.Equal($"/diary/new?movieId={Samples.Id(2)}", CurrentUrl);
    }

    [Fact]
    public async Task Editing_loads_the_entry_and_puts_it_back()
    {
        Backend.Get($"/api/diary-entries/{Entry}", Samples.Entry(1, movie: 2, watchedOn: new DateOnly(2026, 10, 5), rating: 9,
            review: "Fine", isRewatch: true, liked: true, tags: ["a", "b"]));
        Backend.Json(HttpMethod.Put, $"/api/diary-entries/{Entry}", Samples.Entry(1, movie: 2));

        var app = await OpenAsync($"/diary/{Entry}/edit");

        app.WaitForAssertion(() => Assert.Equal("Movie 2 (1982)", app.Find("#movie").GetAttribute("value")));
        Assert.Contains("Edit diary entry", app.Find("h1").TextContent);
        Assert.Equal("2026-10-05", app.Find("#watchedOn").GetAttribute("value"));
        Assert.Equal("9", app.Find("#rating").GetAttribute("value"));
        Assert.Equal("Fine", app.Find("#review").GetAttribute("value"));
        Assert.Equal("a, b", app.Find("#tags").GetAttribute("value"));
        Assert.DoesNotContain("Tags the movie has on your watchlist", app.Markup);
        Assert.Equal("/diary", app.Find("a.is-light").GetAttribute("href"));

        app.Find("#review").Input("Better");
        Submit(app);

        app.WaitForAssertion(() => Assert.Equal($"/movies/{Samples.Id(2)}", CurrentUrl));
        Assert.Equal(
            $$"""{"movieId":"{{Samples.Id(2)}}","watchedOn":"2026-10-05","rating":9,"review":"Better","isRewatch":true,"liked":true,"tags":["a","b"]}""",
            Backend.Last(HttpMethod.Put, $"/api/diary-entries/{Entry}").Body);
        Assert.Empty(Backend.Sent(HttpMethod.Post, "/api/diary-entries"));
    }

    [Fact]
    public async Task An_entry_that_cannot_be_loaded_shows_the_error()
    {
        Backend.Problem(HttpMethod.Get, $"/api/diary-entries/{Entry}", HttpStatusCode.NotFound, "Entry not found");
        await SignInAsync();

        var app = Open($"/diary/{Entry}/edit");

        app.WaitForAssertion(() => Assert.Contains("Entry not found", app.Find(".notification.is-danger").TextContent));
    }
}
