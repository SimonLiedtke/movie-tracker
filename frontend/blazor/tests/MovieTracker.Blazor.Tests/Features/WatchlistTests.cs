using System.Net;
using MovieTracker.Blazor.Tests.Support;

namespace MovieTracker.Blazor.Tests.Features;

public class WatchlistTests : AppTest
{
    private static readonly WatchlistItem[] Items =
    [
        Samples.Item(1, note: "Why not", runtime: 95, "weekend", "classic"),
        Samples.Item(2, runtime: null),
    ];

    public WatchlistTests()
    {
        Backend.Get("/api/watchlist", Samples.WatchlistPage(Items));
        Backend.Get("/api/watchlist/tags", new[] { "weekend", "date night" });
    }

    private async Task<IRenderedComponent<App>> OpenAsync(string url = "/watchlist")
    {
        await SignInAsync();
        var app = Open(url);
        app.WaitForAssertion(() => Assert.NotEmpty(app.FindAll("main .box")));
        return app;
    }

    private static string Count(IRenderedComponent<App> app) =>
        app.Find("p.mb-4.has-text-grey").Text().Replace(" · loading…", "");

    [Fact]
    public async Task Items_show_movie_date_runtime_genres_note_and_tags()
    {
        var app = await OpenAsync();

        app.WaitForAssertion(() => Assert.Equal(2, app.FindAll("article").Count));
        var first = app.FindAll("article")[0];
        Assert.Contains("Movie 1 (1981)", first.Text());
        Assert.Contains("Added 2026-11-03 · 95 min", first.Text());
        Assert.Equal("Comedy", first.QuerySelector(".tags a.tag.is-link")!.TextContent);
        Assert.Contains("Why not", first.Text());
        Assert.Equal(new[] { "classic", "weekend" }.Order(), first.QuerySelectorAll(".tags a.tag.is-light:not(.is-link)").Select(a => a.TextContent).Order());
        Assert.Equal($"/diary/new?movieId={Samples.Id(1)}", first.QuerySelector("a.button.is-primary")!.GetAttribute("href"));
        Assert.DoesNotContain("min", app.FindAll("article")[1].QuerySelector("p.is-size-7")!.TextContent);
        Assert.Equal("2 movies to watch", Count(app));
    }

    [Fact]
    public async Task The_request_carries_the_filters_of_the_URL()
    {
        await OpenAsync("/watchlist?genreId=2&genreId=3&genreMatch=all&releasedFrom=1979-05-25&releasedTo=1989-11-03&addedFrom=2026-01-01"
            + "&addedTo=2026-02-01&runtimeFrom=90&runtimeTo=120&search=alien&tag=weekend&page=2");

        Assert.Equal(
            "?page=2&pageSize=20&genreId=2&genreId=3&genreMatch=all&releasedFrom=1979-05-25&releasedTo=1989-11-03"
            + "&addedFrom=2026-01-01&addedTo=2026-02-01&runtimeFrom=90&runtimeTo=120&search=alien&tag=weekend",
            Backend.Last(HttpMethod.Get, "/api/watchlist").Query);
    }

    [Fact]
    public async Task Parameters_that_are_not_numbers_are_left_out()
    {
        await OpenAsync("/watchlist?genreId=abc&genreId=2&genreId=2&runtimeFrom=-4&runtimeTo=x&page=zzz");

        Assert.Equal("?page=1&pageSize=20&genreId=2&genreMatch=any", Backend.Last(HttpMethod.Get, "/api/watchlist").Query);
    }

    [Fact]
    public async Task A_filter_is_described_with_its_count()
    {
        Backend.Get("/api/watchlist", Samples.WatchlistPage(Items.Take(1).ToList(), total: 1, unfiltered: 7));

        var app = await OpenAsync("/watchlist?genreId=2&genreMatch=all&runtimeFrom=90&releasedFrom=1979-05-25&releasedTo=1989-11-03&addedTo=2026-02-01&search=alien&tag=weekend");

        app.WaitForAssertion(() => Assert.Equal(
            "1 of 7 movies to watch matching \"alien\", tagged \"weekend\", with all of the selected genres, "
            + "runtime of at least 90 minutes, released from May 25, 1979 to November 3, 1989, added until February 1, 2026",
            Count(app)));
    }

    [Fact]
    public async Task Genres_are_buttons_that_toggle_the_filter()
    {
        var app = await OpenAsync();
        app.WaitForAssertion(() => Assert.Equal(3, app.FindAll("[aria-labelledby=genre-label] button").Count));

        app.FindAll("[aria-labelledby=genre-label] button")[1].Click();
        Assert.Equal("/watchlist?genreId=2", CurrentUrl);
        Assert.Equal("true", app.FindAll("[aria-labelledby=genre-label] button")[1].GetAttribute("aria-pressed"));
        Assert.Contains("is-link", app.FindAll("[aria-labelledby=genre-label] button")[1].ClassName);

        app.FindAll("[aria-labelledby=genre-label] button")[2].Click();
        Assert.Equal("/watchlist?genreId=2&genreId=3", CurrentUrl);

        app.FindAll("[aria-labelledby=genre-label] button")[1].Click();
        Assert.Equal("/watchlist?genreId=3", CurrentUrl);
    }

    [Fact]
    public async Task Choosing_a_genre_goes_back_to_the_first_page()
    {
        var app = await OpenAsync("/watchlist?page=3&tag=weekend");
        app.WaitForAssertion(() => Assert.Equal(3, app.FindAll("[aria-labelledby=genre-label] button").Count));

        app.FindAll("[aria-labelledby=genre-label] button")[0].Click();

        Assert.Equal("/watchlist?tag=weekend&genreId=1", CurrentUrl);
    }

    [Fact]
    public async Task The_match_mode_is_in_the_URL_only_when_it_is_all()
    {
        var app = await OpenAsync("/watchlist?genreId=1");

        var buttons = app.FindAll("[aria-labelledby=genre-match-label] button");
        Assert.Equal("true", buttons[0].GetAttribute("aria-pressed"));
        buttons[1].Click();
        Assert.Equal("/watchlist?genreId=1&genreMatch=all", CurrentUrl);

        app.FindAll("[aria-labelledby=genre-match-label] button")[0].Click();
        Assert.Equal("/watchlist?genreId=1", CurrentUrl);
    }

    [Fact]
    public async Task Changing_only_the_match_mode_without_genres_does_not_load_again()
    {
        var app = await OpenAsync();
        var loads = Backend.Sent(HttpMethod.Get, "/api/watchlist").Count();

        app.FindAll("[aria-labelledby=genre-match-label] button")[1].Click();

        Assert.Equal("/watchlist?genreMatch=all", CurrentUrl);
        Assert.Equal(loads, Backend.Sent(HttpMethod.Get, "/api/watchlist").Count());
    }

    [Fact]
    public async Task A_genre_tag_of_an_item_filters_by_that_genre_only()
    {
        var app = await OpenAsync("/watchlist?genreId=1&page=2&tag=weekend");
        app.WaitForAssertion(() => Assert.NotEmpty(app.FindAll("article")));

        Assert.Equal("/watchlist?tag=weekend&genreId=2", app.Find("article .tags a.tag.is-link").GetAttribute("href"));
    }

    [Fact]
    public async Task The_runtime_has_quick_picks_that_toggle()
    {
        var app = await OpenAsync();
        var presets = () => app.FindAll("[aria-labelledby=runtime-label] button");

        presets()[0].Click();
        Assert.Equal("/watchlist?runtimeTo=89", CurrentUrl);
        Assert.Equal("true", presets()[0].GetAttribute("aria-pressed"));

        presets()[1].Click();
        Assert.Equal("/watchlist?runtimeFrom=90&runtimeTo=120", CurrentUrl);

        presets()[2].Click();
        Assert.Equal("/watchlist?runtimeFrom=121", CurrentUrl);

        presets()[2].Click();
        Assert.Equal("/watchlist", CurrentUrl);
    }

    [Fact]
    public async Task The_dates_runtime_and_tag_fields_change_the_URL()
    {
        var app = await OpenAsync();

        app.Find("#runtime-from").Change("90");
        app.Find("#runtime-to").Change("120");
        app.Find("#released-from").Change("1979-05-25");
        app.Find("#released-to").Change("1989-11-03");
        app.Find("#added-from").Change("2026-01-01");
        app.Find("#added-to").Change("2026-02-01");
        app.Find("#tag").Change(" weekend ");

        Assert.Equal(
            "/watchlist?runtimeFrom=90&runtimeTo=120&releasedFrom=1979-05-25&releasedTo=1989-11-03&addedFrom=2026-01-01&addedTo=2026-02-01&tag=weekend",
            CurrentUrl);
        Assert.Equal("1989-11-03", app.Find("#released-from").GetAttribute("max"));
        Assert.Equal("1979-05-25", app.Find("#released-to").GetAttribute("min"));
        Assert.Equal("2026-02-01", app.Find("#added-from").GetAttribute("max"));
        Assert.Equal("2026-01-01", app.Find("#added-to").GetAttribute("min"));
    }

    [Fact]
    public async Task The_tag_field_suggests_the_known_tags_and_the_tags_shown()
    {
        var app = await OpenAsync();

        app.WaitForAssertion(() => Assert.Equal(["classic", "date night", "weekend"], app.FindAll("#tag-suggestions option").Select(o => o.GetAttribute("value"))));
    }

    [Fact]
    public async Task The_title_search_waits_for_a_pause_and_ignores_blank_text()
    {
        var app = await OpenAsync();

        app.Find("#search").Input("alien");
        app.WaitForAssertion(() => Assert.Equal("/watchlist?search=alien", CurrentUrl), TimeSpan.FromSeconds(3));
        Assert.Equal("alien", app.Find("#search").GetAttribute("value"));

        app.Find("#search").Input("   ");
        app.WaitForAssertion(() => Assert.Equal("/watchlist", CurrentUrl), TimeSpan.FromSeconds(3));
    }

    [Fact]
    public async Task The_filter_can_be_cleared_at_once()
    {
        var app = await OpenAsync("/watchlist?genreId=1&search=a&tag=b&runtimeFrom=90&page=2");

        app.Find(".box button.is-light").Click();

        Assert.Equal("/watchlist", CurrentUrl);
        app.WaitForAssertion(() => Assert.Empty(app.FindAll(".box button.is-light")));
    }

    [Fact]
    public async Task An_empty_result_says_why()
    {
        Backend.Get("/api/watchlist", Samples.WatchlistPage([], total: 0, unfiltered: 5));
        var app = await OpenAsync("/watchlist?tag=none");

        app.WaitForAssertion(() => Assert.Contains("No movie on your watchlist matches the filter.", app.Find(".notification").Text()));
        app.Find(".notification button").Click();
        Assert.Equal("/watchlist", CurrentUrl);
    }

    [Fact]
    public async Task An_empty_watchlist_says_so()
    {
        Backend.Get("/api/watchlist", Samples.WatchlistPage([]));
        var app = await OpenAsync();

        app.WaitForAssertion(() => Assert.Equal("Your watchlist is empty. Search a movie above to add it.", app.Find(".notification").Text()));
    }

    [Fact]
    public async Task A_page_past_the_end_goes_to_the_last_page()
    {
        Backend.On(HttpMethod.Get, "/api/watchlist", request =>
            FakeBackend.JsonResponse(request.Query.Contains("page=9")
                ? new WatchlistPage([], 9, 20, 41, 41)
                : Samples.WatchlistPage(Items, page: 3, total: 41, unfiltered: 41)));
        await SignInAsync();

        Open("/watchlist?page=9");

        Assert.Equal("/watchlist?page=3", CurrentUrl);
    }

    [Fact]
    public async Task The_note_and_tags_of_an_item_are_edited_and_saved()
    {
        Backend.Json(HttpMethod.Put, $"/api/watchlist/{Samples.Id(1)}", Samples.Item(1, "New note", 95, "a", "b"));
        var app = await OpenAsync();
        app.WaitForAssertion(() => Assert.NotEmpty(app.FindAll("article")));
        Assert.Equal("Edit note and tags", app.FindAll("article")[0].QuerySelectorAll(".media-right button")[0].TextContent.Trim());
        Assert.Equal("Add note or tags", app.FindAll("article")[1].QuerySelectorAll(".media-right button")[0].TextContent.Trim());

        app.FindAll("article")[0].QuerySelectorAll(".media-right button")[0].Click();
        Assert.Equal("Why not", app.Find("input[aria-label=Note]").GetAttribute("value"));
        Assert.Equal("weekend, classic", app.Find("input[aria-label=Tags]").GetAttribute("value"));
        app.Find("input[aria-label=Note]").Input("  New note ");
        app.Find("input[aria-label=Tags]").Input("a, b");
        app.Find("article form").Submit();

        app.WaitForAssertion(() => Assert.Empty(app.FindAll("article form")));
        Assert.Equal("""{"note":"New note","tags":["a","b"]}""", Backend.Last(HttpMethod.Put, $"/api/watchlist/{Samples.Id(1)}").Body);
        Assert.Contains("New note", app.FindAll("article")[0].Text());
        Assert.Equal(["a", "b"], app.FindAll("article")[0].QuerySelectorAll("a.tag:not(.is-link)").Select(a => a.TextContent));
        Assert.Single(Backend.Sent(HttpMethod.Get, "/api/watchlist"));
    }

    [Fact]
    public async Task A_blank_note_is_sent_as_null()
    {
        Backend.Json(HttpMethod.Put, $"/api/watchlist/{Samples.Id(2)}", Samples.Item(2));
        var app = await OpenAsync();
        app.WaitForAssertion(() => Assert.NotEmpty(app.FindAll("article")));

        app.FindAll("article")[1].QuerySelectorAll(".media-right button")[0].Click();
        app.Find("input[aria-label=Note]").Input("   ");
        app.Find("article form").Submit();

        app.WaitForAssertion(() => Assert.Single(Backend.Sent(HttpMethod.Put, $"/api/watchlist/{Samples.Id(2)}")));
        Assert.Equal("""{"note":null,"tags":[]}""", Backend.Last(HttpMethod.Put, $"/api/watchlist/{Samples.Id(2)}").Body);
    }

    [Fact]
    public async Task Tags_the_server_would_refuse_are_refused_before_sending()
    {
        var app = await OpenAsync();
        app.WaitForAssertion(() => Assert.NotEmpty(app.FindAll("article")));
        app.FindAll("article")[0].QuerySelectorAll(".media-right button")[0].Click();

        app.Find("input[aria-label=Tags]").Input(new string('x', 51));
        app.Find("article form").Submit();

        Assert.Equal("A tag can have at most 50 characters.", app.Find("article .help.is-danger").TextContent);
        Assert.Empty(Backend.Sent(HttpMethod.Put, $"/api/watchlist/{Samples.Id(1)}"));
    }

    [Fact]
    public async Task Editing_can_be_cancelled_with_the_button_and_with_escape()
    {
        var app = await OpenAsync();
        app.WaitForAssertion(() => Assert.NotEmpty(app.FindAll("article")));

        app.FindAll("article")[0].QuerySelectorAll(".media-right button")[0].Click();
        app.Find("article form button[type=button]").Click();
        Assert.Empty(app.FindAll("article form"));

        app.FindAll("article")[0].QuerySelectorAll(".media-right button")[0].Click();
        app.Find("input[aria-label=Note]").KeyDown(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "Escape" });
        Assert.Empty(app.FindAll("article form"));
        Assert.Empty(Backend.Sent(HttpMethod.Put, $"/api/watchlist/{Samples.Id(1)}"));
    }

    [Fact]
    public async Task An_item_is_removed_and_the_list_is_loaded_again()
    {
        Backend.NoContent(HttpMethod.Delete, $"/api/watchlist/{Samples.Id(1)}");
        var app = await OpenAsync();
        app.WaitForAssertion(() => Assert.NotEmpty(app.FindAll("article")));

        app.FindAll("article")[0].QuerySelector("button.is-danger")!.Click();

        app.WaitForAssertion(() => Assert.Equal(2, Backend.Sent(HttpMethod.Get, "/api/watchlist").Count()));
        Assert.Single(Backend.Sent(HttpMethod.Delete, $"/api/watchlist/{Samples.Id(1)}"));
    }

    [Fact]
    public async Task A_failed_removal_shows_the_error()
    {
        Backend.Problem(HttpMethod.Delete, $"/api/watchlist/{Samples.Id(1)}", HttpStatusCode.InternalServerError, "Cannot remove");
        var app = await OpenAsync();
        app.WaitForAssertion(() => Assert.NotEmpty(app.FindAll("article")));

        app.FindAll("article")[0].QuerySelector("button.is-danger")!.Click();

        app.WaitForAssertion(() => Assert.Contains("Cannot remove", app.Find(".notification.is-danger").TextContent));
    }

    [Fact]
    public async Task A_picked_movie_is_added_unless_it_is_on_the_watchlist_already()
    {
        Backend.Get($"/api/movies/{Samples.Id(7)}", Samples.Movie(7));
        Backend.Get($"/api/movies/{Samples.Id(8)}", Samples.Movie(8, onWatchlist: true));
        Backend.Json(HttpMethod.Put, $"/api/watchlist/{Samples.Id(7)}", Samples.Item(7));
        Backend.Get("/api/movies/search", new MovieSearchResponse(
            [
                new MovieSearchResult(Samples.Id(7), null, "Seven", null, null, null),
                new MovieSearchResult(Samples.Id(8), null, "Eight", null, null, null),
            ], true));
        var app = await OpenAsync();

        app.Find("#add").Input("e");
        app.WaitForAssertion(() => Assert.Equal(2, app.FindAll(".dropdown-content button").Count), TimeSpan.FromSeconds(3));
        app.FindAll(".dropdown-content button")[0].Click();
        app.WaitForAssertion(() => Assert.Single(Backend.Sent(HttpMethod.Put, $"/api/watchlist/{Samples.Id(7)}")));
        Assert.Equal("""{"note":null,"tags":[]}""", Backend.Last(HttpMethod.Put, $"/api/watchlist/{Samples.Id(7)}").Body);
        app.WaitForAssertion(() => Assert.Equal(2, Backend.Sent(HttpMethod.Get, "/api/watchlist").Count()));

        app.Find("#add").Input("ei");
        app.WaitForAssertion(() => Assert.Equal(2, app.FindAll(".dropdown-content button").Count), TimeSpan.FromSeconds(3));
        app.FindAll(".dropdown-content button")[1].Click();
        app.WaitForAssertion(() => Assert.Single(Backend.Sent(HttpMethod.Get, $"/api/movies/{Samples.Id(8)}")));

        Assert.Empty(Backend.Sent(HttpMethod.Put, $"/api/watchlist/{Samples.Id(8)}"));
        // The field is empty again, ready for the next movie.
        Assert.Equal("", app.Find("#add").GetAttribute("value"));
    }

    [Fact]
    public async Task A_failed_request_shows_the_error()
    {
        Backend.Problem(HttpMethod.Get, "/api/watchlist", HttpStatusCode.InternalServerError, "Database is down");
        var app = await OpenAsync();

        app.WaitForAssertion(() => Assert.Contains("Database is down", app.Find(".notification.is-danger").TextContent));
    }
}
