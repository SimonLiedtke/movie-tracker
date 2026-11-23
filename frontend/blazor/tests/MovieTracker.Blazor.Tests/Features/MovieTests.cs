using System.Net;
using MovieTracker.Blazor.Tests.Support;

namespace MovieTracker.Blazor.Tests.Features;

public class MovieListTests : AppTest
{
    public MovieListTests()
    {
        Backend.Get("/api/movies", Samples.Page([Samples.Summary(1, rating: 7, watchCount: 2, onWatchlist: true), Samples.Summary(2)], pageSize: 24, total: 60));
    }

    private async Task<IRenderedComponent<App>> OpenAsync(string url)
    {
        await SignInAsync();
        return Open(url);
    }

    [Fact]
    public async Task Movies_are_shown_as_cards()
    {
        var app = await OpenAsync("/movies");

        app.WaitForAssertion(() => Assert.Equal(2, app.FindAll(".card").Count));
        var first = app.FindAll(".card")[0];
        Assert.Equal($"/movies/{Samples.Id(1)}", first.GetAttribute("href"));
        Assert.Contains("Movie 1", first.TextContent);
        Assert.Contains("1981", first.TextContent);
        Assert.Contains("· watched 2×", first.TextContent);
        Assert.Contains("★★★½", first.TextContent);
        Assert.Contains("Watchlist", first.TextContent);
        Assert.Contains("60 movies", app.Find("main").TextContent);
    }

    [Fact]
    public async Task The_list_asks_for_the_first_page_sorted_by_title_by_default()
    {
        await OpenAsync("/movies");

        Assert.Equal("?sort=title&page=1&pageSize=24", Backend.Last(HttpMethod.Get, "/api/movies").Query);
    }

    [Fact]
    public async Task The_fields_show_the_filters_of_the_URL_and_the_request_carries_them()
    {
        var app = await OpenAsync("/movies?search=alien&genreId=2&year=1986&sort=-releaseDate&page=3");

        Assert.Equal("?search=alien&genreId=2&year=1986&sort=-releaseDate&page=3&pageSize=24", Backend.Last(HttpMethod.Get, "/api/movies").Query);
        Assert.Equal("alien", app.Find("#search").GetAttribute("value"));
        Assert.Equal("1986", app.Find("#year").GetAttribute("value"));
        app.WaitForAssertion(() => Assert.Equal("2", app.Find("#genre").GetAttribute("value")));
        Assert.Equal("-releaseDate", app.Find("#sort").GetAttribute("value"));
    }

    [Fact]
    public async Task An_unknown_sort_falls_back_to_the_title()
    {
        await OpenAsync("/movies?sort=nonsense&genreId=abc");

        Assert.Equal("?sort=title&page=1&pageSize=24", Backend.Last(HttpMethod.Get, "/api/movies").Query);
    }

    [Fact]
    public async Task Choosing_a_genre_goes_back_to_the_first_page()
    {
        var app = await OpenAsync("/movies?page=3&search=a");
        app.WaitForAssertion(() => Assert.Equal(4, app.FindAll("#genre option").Count));

        app.Find("#genre").Change("2");

        Assert.Equal("/movies?search=a&genreId=2", CurrentUrl);
        app.WaitForAssertion(() => Assert.Equal("?search=a&genreId=2&sort=title&page=1&pageSize=24", Backend.Last(HttpMethod.Get, "/api/movies").Query));
    }

    [Fact]
    public async Task Choosing_a_sort_order_and_a_year_changes_the_URL()
    {
        var app = await OpenAsync("/movies");

        app.Find("#sort").Change("-createdAt");
        Assert.Equal("/movies?sort=-createdAt", CurrentUrl);

        app.Find("#year").Change("1999");
        Assert.Equal("/movies?sort=-createdAt&year=1999", CurrentUrl);

        app.Find("#year").Change("");
        Assert.Equal("/movies?sort=-createdAt", CurrentUrl);
    }

    [Fact]
    public async Task Typing_a_search_waits_for_a_pause_and_replaces_the_history_entry()
    {
        var app = await OpenAsync("/movies");

        app.Find("#search").Input("blade");

        Assert.Equal("/movies", CurrentUrl);
        app.WaitForAssertion(() => Assert.Equal("/movies?search=blade", CurrentUrl), TimeSpan.FromSeconds(3));
        Assert.Equal("blade", app.Find("#search").GetAttribute("value"));
        Assert.True(Navigation.History.Last().Options.ReplaceHistoryEntry);
    }

    [Fact]
    public async Task Paging_keeps_the_filters()
    {
        var app = await OpenAsync("/movies?search=a");
        app.WaitForAssertion(() => Assert.NotEmpty(app.FindAll(".pagination")));

        app.Find(".pagination-next").Click();

        Assert.Equal("/movies?search=a&page=2", CurrentUrl);
    }

    [Fact]
    public async Task Nothing_found_offers_to_clear_the_filters()
    {
        Backend.Get("/api/movies", Samples.Page<MovieSummary>([], pageSize: 24));
        var app = await OpenAsync("/movies?search=zzz");

        app.WaitForAssertion(() => Assert.Contains("No movies found.", app.Find(".notification").TextContent));
        app.Find(".notification button").Click();

        Assert.Equal("/movies", CurrentUrl);
    }

    [Fact]
    public async Task An_empty_list_without_filters_points_to_the_search()
    {
        Backend.Get("/api/movies", Samples.Page<MovieSummary>([], pageSize: 24));
        var app = await OpenAsync("/movies");

        app.WaitForAssertion(() => Assert.Contains("Find a movie above", app.Find(".notification").TextContent));
        Assert.Empty(app.FindAll(".notification button"));
    }

    [Fact]
    public async Task A_failed_request_shows_the_error_and_can_be_dismissed()
    {
        Backend.Problem(HttpMethod.Get, "/api/movies", HttpStatusCode.InternalServerError, "Database is down");
        var app = await OpenAsync("/movies");

        app.WaitForAssertion(() => Assert.Contains("Database is down", app.Find(".notification.is-danger").TextContent));
        app.Find(".notification.is-danger .delete").Click();

        Assert.Empty(app.FindAll(".notification.is-danger"));
    }

    [Fact]
    public async Task Without_genres_the_filter_has_no_choices()
    {
        Backend.Problem(HttpMethod.Get, "/api/genres", HttpStatusCode.InternalServerError, "Broken");
        var app = await OpenAsync("/movies");

        app.WaitForAssertion(() => Assert.Equal(2, app.FindAll(".card").Count));
        Assert.Single(app.FindAll("#genre option"));
        Assert.Empty(app.FindAll(".notification.is-danger"));
    }

    [Fact]
    public async Task Picking_a_movie_in_the_search_opens_it()
    {
        Backend.Get("/api/movies/search", new MovieSearchResponse(
            [new MovieSearchResult(Samples.Id(7), null, "Seven", null, new DateOnly(1995, 9, 22), null)], true));
        var app = await OpenAsync("/movies");

        app.Find("#find").Input("sev");
        app.WaitForAssertion(() => Assert.Single(app.FindAll(".dropdown-content button")), TimeSpan.FromSeconds(3));
        app.Find(".dropdown-content button").Click();

        app.WaitForAssertion(() => Assert.Equal($"/movies/{Samples.Id(7)}", CurrentUrl));
    }
}

public class MoviePickerTests : AppTest
{
    private static readonly MovieSearchResult Manual = new(Samples.Id(3), null, "My own film", null, null, null);
    private static readonly MovieSearchResult Tmdb = new(null, 603, "The Matrix", "The Matrix", new DateOnly(1999, 3, 31), "https://img.test/m.jpg");

    private async Task<IRenderedComponent<App>> OpenDiaryFormAsync()
    {
        await SignInAsync();
        return Open("/diary/new");
    }

    private static void Search(IRenderedComponent<App> app, string text)
    {
        app.Find("#movie").Input(text);
        app.WaitForAssertion(() => Assert.NotEmpty(app.FindAll(".dropdown-content .dropdown-item")), TimeSpan.FromSeconds(3));
    }

    [Fact]
    public async Task Results_show_the_year_and_mark_the_users_own_movies()
    {
        Backend.Get("/api/movies/search", new MovieSearchResponse([Manual, Tmdb], true));
        var app = await OpenDiaryFormAsync();

        Search(app, "m");

        var items = app.FindAll(".dropdown-content button");
        Assert.Contains("My own film", items[0].TextContent);
        Assert.Equal("Yours", items[0].QuerySelector(".tag")!.TextContent);
        Assert.Contains("The Matrix (1999)", items[1].TextContent);
        Assert.Empty(items[1].QuerySelectorAll(".tag"));
        Assert.Single(app.FindAll(".dropdown.is-active"));
        Assert.Equal("?query=m", Backend.Last(HttpMethod.Get, "/api/movies/search").Query);
    }

    [Fact]
    public async Task The_search_waits_for_a_pause_in_typing()
    {
        Backend.Get("/api/movies/search", new MovieSearchResponse([Manual], true));
        var app = await OpenDiaryFormAsync();

        app.Find("#movie").Input("a");
        app.Find("#movie").Input("ab");
        app.Find("#movie").Input("abc");
        app.WaitForAssertion(() => Assert.NotEmpty(app.FindAll(".dropdown-content button")), TimeSpan.FromSeconds(3));

        Assert.Equal("?query=abc", Assert.Single(Backend.Sent(HttpMethod.Get, "/api/movies/search")).Query);
    }

    [Fact]
    public async Task Without_tmdb_the_user_is_told_that_only_their_own_movies_are_shown()
    {
        Backend.Get("/api/movies/search", new MovieSearchResponse([Manual], false));
        var app = await OpenDiaryFormAsync();

        Search(app, "m");

        Assert.Contains("TMDB search is not available right now", app.Find(".dropdown-content").TextContent);
    }

    [Fact]
    public async Task A_failed_search_shows_no_movies()
    {
        Backend.Problem(HttpMethod.Get, "/api/movies/search", HttpStatusCode.BadGateway, "TMDB down");
        var app = await OpenDiaryFormAsync();

        Search(app, "m");

        Assert.Contains("No matching movies.", app.Find(".dropdown-content").TextContent);
    }

    [Fact]
    public async Task The_manual_add_link_carries_the_search_text_and_where_to_continue()
    {
        Backend.Get("/api/movies/search", new MovieSearchResponse([], true));
        var app = await OpenDiaryFormAsync();

        Search(app, "  My Film ");

        Assert.Equal("/movies/new?title=My%20Film&next=diary", app.Find(".dropdown-content a").GetAttribute("href"));
    }

    [Fact]
    public async Task Picking_a_manual_movie_selects_it_without_a_request()
    {
        Backend.Get("/api/movies/search", new MovieSearchResponse([Manual, Tmdb], true));
        var app = await OpenDiaryFormAsync();
        Search(app, "m");

        app.FindAll(".dropdown-content button")[0].Click();

        app.WaitForAssertion(() => Assert.True(app.Find("#movie").HasAttribute("readonly")));
        Assert.Equal("My own film", app.Find("#movie").GetAttribute("value"));
        Assert.Empty(Backend.Sent(HttpMethod.Post, $"/api/movies/tmdb/{Tmdb.TmdbId}"));
    }

    [Fact]
    public async Task Picking_a_tmdb_movie_imports_it_first()
    {
        Backend.Get("/api/movies/search", new MovieSearchResponse([Tmdb], true));
        Backend.Json(HttpMethod.Post, "/api/movies/tmdb/603", Samples.Movie(8, tmdbId: 603) with { Title = "The Matrix", ReleaseDate = new DateOnly(1999, 3, 31) });
        var app = await OpenDiaryFormAsync();
        Search(app, "matrix");

        app.Find(".dropdown-content button").Click();

        app.WaitForAssertion(() => Assert.Equal("The Matrix (1999)", app.Find("#movie").GetAttribute("value")));
        Assert.Single(Backend.Sent(HttpMethod.Post, "/api/movies/tmdb/603"));
    }

    [Fact]
    public async Task A_failed_import_shows_the_error_and_keeps_the_search()
    {
        Backend.Get("/api/movies/search", new MovieSearchResponse([Tmdb], true));
        Backend.Problem(HttpMethod.Post, "/api/movies/tmdb/603", HttpStatusCode.BadGateway, "TMDB did not answer");
        var app = await OpenDiaryFormAsync();
        Search(app, "matrix");

        app.Find(".dropdown-content button").Click();

        app.WaitForAssertion(() => Assert.Equal("TMDB did not answer", app.Find("p.help.is-danger").TextContent));
        Assert.False(app.Find("#movie").HasAttribute("readonly"));
    }

    [Fact]
    public async Task A_selected_movie_can_be_changed()
    {
        Backend.Get("/api/movies/search", new MovieSearchResponse([Manual], true));
        var app = await OpenDiaryFormAsync();
        Search(app, "m");
        app.Find(".dropdown-content button").Click();
        app.WaitForAssertion(() => Assert.True(app.Find("#movie").HasAttribute("readonly")));

        app.Find(".field.has-addons button").Click();

        app.WaitForAssertion(() => Assert.False(app.Find("#movie").HasAttribute("readonly")));
        Assert.Equal("", app.Find("#movie").GetAttribute("value"));
    }

    [Fact]
    public async Task Clearing_the_text_closes_the_results()
    {
        Backend.Get("/api/movies/search", new MovieSearchResponse([Manual], true));
        var app = await OpenDiaryFormAsync();
        Search(app, "m");

        app.Find("#movie").Input("");

        app.WaitForAssertion(() => Assert.Empty(app.FindAll(".dropdown.is-active")), TimeSpan.FromSeconds(3));
    }

    [Fact]
    public async Task Escape_closes_the_results()
    {
        Backend.Get("/api/movies/search", new MovieSearchResponse([Manual], true));
        var app = await OpenDiaryFormAsync();
        Search(app, "m");

        app.Find("#movie").KeyDown(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "Escape" });

        Assert.Empty(app.FindAll(".dropdown.is-active"));
    }
}
