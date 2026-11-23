using System.Net;
using MovieTracker.Blazor.Tests.Support;

namespace MovieTracker.Blazor.Tests.Features;

public class MovieDetailTests : AppTest
{
    private static string Url(int movie) => $"/api/movies/{Samples.Id(movie)}";

    private async Task<IRenderedComponent<App>> OpenAsync(Movie movie, params DiaryEntry[] entries)
    {
        Backend.Get(Url(1), movie);
        Backend.Get(Url(1) + "/diary-entries", entries);
        await SignInAsync();
        var app = Open($"/movies/{Samples.Id(1)}");
        app.WaitForAssertion(() => Assert.NotEmpty(app.FindAll("h1")));
        return app;
    }

    [Fact]
    public async Task A_tmdb_movie_is_shown_without_edit_and_delete()
    {
        var movie = Samples.Movie(1, tmdbId: 603, watchCount: 2, originalTitle: "Matrix, The") with { LatestRating = 9 };

        var app = await OpenAsync(movie);

        Assert.Contains("Movie 1", app.Find("h1").TextContent);
        Assert.Contains("(1981)", app.Find("h1").TextContent);
        Assert.Contains("Original title: Matrix, The", app.Find("main").TextContent);
        Assert.Equal(["Action", "Drama"], app.FindAll(".tags a.tag.is-link").Select(a => a.TextContent));
        Assert.Equal("/movies?genreId=1", app.Find(".tags a.tag.is-link").GetAttribute("href"));
        Assert.Contains("Overview 1", app.Find("main").TextContent);
        Assert.Contains("1981-05-17", app.Find("main").TextContent);
        Assert.Contains("101 min", app.Find("main").TextContent);
        Assert.Equal("https://www.themoviedb.org/movie/603", app.Find("a[title='Details from TMDB']").GetAttribute("href"));
        Assert.Contains("★★★★½", app.Find("main").TextContent);
        Assert.Equal(["Log a viewing", "Add to watchlist"], app.FindAll(".buttons > *").Select(b => b.TextContent.Trim()));
        Assert.Equal($"/diary/new?movieId={Samples.Id(1)}", app.Find(".buttons a.is-primary").GetAttribute("href"));
        Assert.Contains("Not watched yet.", app.Markup);
    }

    [Fact]
    public async Task The_original_title_is_left_out_when_it_is_the_title()
    {
        var app = await OpenAsync(Samples.Movie(1, originalTitle: "Movie 1"));

        Assert.DoesNotContain("Original title", app.Markup);
    }

    [Fact]
    public async Task A_manual_movie_can_be_edited_and_deleted()
    {
        var app = await OpenAsync(Samples.Movie(1, tmdbId: null));

        Assert.Contains("Added manually", app.Markup);
        Assert.Equal(["Log a viewing", "Add to watchlist", "Edit", "Delete"], app.FindAll(".buttons > *").Select(b => b.TextContent.Trim()));
        Assert.Equal($"/movies/{Samples.Id(1)}/edit", app.FindAll(".buttons a.button")[1].GetAttribute("href"));
    }

    [Fact]
    public async Task The_viewings_are_listed_with_rating_review_and_tags()
    {
        var entries = new[]
        {
            Samples.Entry(1, watchedOn: new DateOnly(2026, 11, 7), rating: 8, review: "Great", isRewatch: true, liked: true, tags: ["cinema"]),
            Samples.Entry(2, watchedOn: new DateOnly(2026, 10, 1)),
        };

        var app = await OpenAsync(Samples.Movie(1), entries);

        var rows = app.FindAll("tbody tr");
        Assert.Equal(2, rows.Count);
        Assert.Contains("2026-11-07", rows[0].TextContent);
        Assert.Contains("↻", rows[0].TextContent);
        Assert.Contains("♥", rows[0].TextContent);
        Assert.Contains("★★★★", rows[0].TextContent);
        Assert.Contains("Great", rows[0].TextContent);
        Assert.Equal("/diary?tag=cinema", rows[0].QuerySelector("a.tag")!.GetAttribute("href"));
        Assert.Equal($"/diary/{Samples.Id(1001)}/edit", rows[0].QuerySelector("a.button")!.GetAttribute("href"));
        Assert.DoesNotContain("↻", rows[1].TextContent);
    }

    [Fact]
    public async Task A_movie_that_does_not_exist_is_said_so()
    {
        Backend.Problem(HttpMethod.Get, Url(1), HttpStatusCode.NotFound, "Not found");
        Backend.Problem(HttpMethod.Get, Url(1) + "/diary-entries", HttpStatusCode.NotFound, "Not found");
        await SignInAsync();

        var app = Open($"/movies/{Samples.Id(1)}");

        app.WaitForAssertion(() => Assert.Contains("This movie does not exist (anymore).", app.Find(".notification").TextContent));
        Assert.Equal("/movies", app.Find(".notification a").GetAttribute("href"));
        Assert.Empty(app.FindAll(".notification.is-danger"));
    }

    [Fact]
    public async Task Something_that_is_no_movie_id_does_not_exist_either()
    {
        await SignInAsync();

        var app = Open("/movies/not-an-id");

        app.WaitForAssertion(() => Assert.Contains("This movie does not exist (anymore).", app.Markup));
    }

    [Fact]
    public async Task Another_error_is_shown_as_it_is()
    {
        Backend.Problem(HttpMethod.Get, Url(1), HttpStatusCode.InternalServerError, "Database is down");
        Backend.Get(Url(1) + "/diary-entries", Array.Empty<DiaryEntry>());
        await SignInAsync();

        var app = Open($"/movies/{Samples.Id(1)}");

        app.WaitForAssertion(() => Assert.Contains("Database is down", app.Find(".notification.is-danger").TextContent));
    }

    [Fact]
    public async Task A_movie_is_put_on_the_watchlist_and_taken_off_again()
    {
        Backend.Json(HttpMethod.Put, "/api/watchlist/" + Samples.Id(1), Samples.Item(1));
        Backend.NoContent(HttpMethod.Delete, "/api/watchlist/" + Samples.Id(1));
        var app = await OpenAsync(Samples.Movie(1));

        app.Find(".buttons button").Click();

        app.WaitForAssertion(() => Assert.Equal("Remove from watchlist", app.Find(".buttons button").TextContent.Trim()));
        Assert.Equal("""{"note":null,"tags":[]}""", Backend.Last(HttpMethod.Put, "/api/watchlist/" + Samples.Id(1)).Body);

        app.Find(".buttons button").Click();

        app.WaitForAssertion(() => Assert.Equal("Add to watchlist", app.Find(".buttons button").TextContent.Trim()));
        Assert.Single(Backend.Sent(HttpMethod.Delete, "/api/watchlist/" + Samples.Id(1)));
    }

    [Fact]
    public async Task A_failed_watchlist_change_shows_the_error_and_changes_nothing()
    {
        Backend.Problem(HttpMethod.Put, "/api/watchlist/" + Samples.Id(1), HttpStatusCode.InternalServerError, "Broken");
        var app = await OpenAsync(Samples.Movie(1));

        app.Find(".buttons button").Click();

        app.WaitForAssertion(() => Assert.Contains("Broken", app.Find(".notification.is-danger").TextContent));
        Assert.Equal("Add to watchlist", app.Find(".buttons button").TextContent.Trim());
    }

    [Fact]
    public async Task A_manual_movie_is_deleted_after_confirming()
    {
        AnswerConfirm(true);
        Backend.NoContent(HttpMethod.Delete, Url(1));
        Backend.Get("/api/movies", Samples.Page<MovieSummary>([], pageSize: 24));
        var app = await OpenAsync(Samples.Movie(1, tmdbId: null));

        app.FindAll(".buttons button")[1].Click();

        app.WaitForAssertion(() => Assert.Equal("/movies", CurrentUrl));
        Assert.Single(Backend.Sent(HttpMethod.Delete, Url(1)));
        var question = Assert.Single(JSInterop.Invocations["confirm"]).Arguments[0];
        Assert.Equal("Delete \"Movie 1\"? This cannot be undone.", question);
    }

    [Fact]
    public async Task A_movie_is_not_deleted_when_the_user_does_not_confirm()
    {
        AnswerConfirm(false);
        var app = await OpenAsync(Samples.Movie(1, tmdbId: null));

        app.FindAll(".buttons button")[1].Click();

        app.WaitForAssertion(() => Assert.NotEmpty(JSInterop.Invocations["confirm"]));
        Assert.Empty(Backend.Sent(HttpMethod.Delete, Url(1)));
        Assert.Equal($"/movies/{Samples.Id(1)}", CurrentUrl);
    }

    [Fact]
    public async Task A_viewing_is_deleted_and_the_movie_is_loaded_again()
    {
        AnswerConfirm(true);
        Backend.NoContent(HttpMethod.Delete, $"/api/diary-entries/{Samples.Id(1001)}");
        var app = await OpenAsync(Samples.Movie(1), Samples.Entry(1));

        app.Find("tbody button").Click();

        app.WaitForAssertion(() => Assert.Equal(2, Backend.Sent(HttpMethod.Get, Url(1)).Count()));
        Assert.Single(Backend.Sent(HttpMethod.Delete, $"/api/diary-entries/{Samples.Id(1001)}"));
        Assert.Equal("Delete the diary entry from 2026-11-07?", JSInterop.Invocations["confirm"].Single().Arguments[0]);
    }
}
