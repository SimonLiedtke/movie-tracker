using System.Net;
using Microsoft.AspNetCore.Components.Forms;
using MovieTracker.Blazor.Tests.Support;

namespace MovieTracker.Blazor.Tests.Features;

public class MovieFormTests : AppTest
{
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 1, 2, 3];

    public MovieFormTests()
    {
        Backend.Json(HttpMethod.Post, "/api/movies", Samples.Movie(5, tmdbId: null), HttpStatusCode.Created);
    }

    private async Task<IRenderedComponent<App>> OpenAsync(string url)
    {
        await SignInAsync();
        var app = Open(url);
        app.WaitForAssertion(() => Assert.NotEmpty(app.FindAll("form")));
        return app;
    }

    private static void Fill(IRenderedComponent<App> app, string title = "My film")
    {
        app.Find("#title").Input(title);
    }

    private static void Submit(IRenderedComponent<App> app) => app.Find("form").Submit();

    private static void Upload(IRenderedComponent<App> app, string name = "poster.png", string type = "image/png", byte[]? content = null) =>
        app.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromBinary(content ?? Png, name, null, type));

    [Fact]
    public async Task The_title_starts_with_what_was_searched_for()
    {
        var app = await OpenAsync("/movies/new?title=My%20film");

        Assert.Equal("My film", app.Find("#title").GetAttribute("value"));
        Assert.Contains("Add a movie manually", app.Find("h1").TextContent);
    }

    [Fact]
    public async Task Problems_are_only_shown_for_fields_that_were_touched_or_when_saving()
    {
        var app = await OpenAsync("/movies/new");
        Assert.Empty(app.FindAll(".help.is-danger"));

        Submit(app);

        Assert.Equal("Please enter a title (at most 300 characters).", Assert.Single(app.FindAll(".help.is-danger")).TextContent);
        Assert.Empty(Backend.Sent(HttpMethod.Post, "/api/movies"));
    }

    [Fact]
    public async Task A_title_that_is_too_long_is_refused()
    {
        var app = await OpenAsync("/movies/new");

        app.Find("#title").Input(new string('x', 301));

        Assert.Contains("Please enter a title", app.Find(".help.is-danger").TextContent);
    }

    [Fact]
    public async Task The_runtime_must_be_between_1_and_1500_minutes()
    {
        var app = await OpenAsync("/movies/new");
        Fill(app);

        foreach (var invalid in new[] { "0", "1501", "1.5", "abc" })
        {
            app.Find("#runtimeMinutes").Input(invalid);
            Assert.Equal("Between 1 and 1500 minutes.", app.Find(".help.is-danger").TextContent);
        }

        app.Find("#runtimeMinutes").Input("1500");
        Assert.Empty(app.FindAll(".help.is-danger"));
        app.Find("#runtimeMinutes").Input("");
        Assert.Empty(app.FindAll(".help.is-danger"));
    }

    [Theory]
    [InlineData("ftp://example.com/p.jpg")]
    [InlineData("example.com/p.jpg")]
    [InlineData("https://")]
    [InlineData("https://exa mple.com")]
    public async Task A_poster_url_must_be_an_http_url(string url)
    {
        var app = await OpenAsync("/movies/new");

        app.Find("#posterUrl").Input(url);

        Assert.Equal("Please enter a valid http(s) URL.", app.Find(".help.is-danger").TextContent);
    }

    [Fact]
    public async Task A_valid_poster_url_is_previewed()
    {
        var app = await OpenAsync("/movies/new");

        app.Find("#posterUrl").Input("https://img.example.com/p.jpg");

        Assert.Equal("https://img.example.com/p.jpg", app.Find(".poster-field img").GetAttribute("src"));
    }

    [Fact]
    public async Task A_movie_is_added_with_what_was_entered()
    {
        var app = await OpenAsync("/movies/new");
        app.WaitForAssertion(() => Assert.Equal(3, app.FindAll("input[type=checkbox]").Count));
        Fill(app, "  My film ");
        app.Find("#originalTitle").Input("Mon film");
        app.Find("#releaseDate").Change("2020-02-03");
        app.Find("#runtimeMinutes").Input("95");
        app.Find("#overview").Input(" A story. ");
        app.Find("#posterUrl").Input("https://img.example.com/p.jpg");
        app.FindAll("input[type=checkbox]")[2].Change(true);
        app.FindAll("input[type=checkbox]")[0].Change(true);

        Submit(app);

        app.WaitForAssertion(() => Assert.Equal($"/movies/{Samples.Id(5)}", CurrentUrl));
        Assert.Equal(
            """{"title":"My film","originalTitle":"Mon film","releaseDate":"2020-02-03","runtimeMinutes":95,"overview":"A story.","posterUrl":"https://img.example.com/p.jpg","genreIds":[3,1]}""",
            Backend.Last(HttpMethod.Post, "/api/movies").Body);
        Assert.Empty(Backend.Sent(HttpMethod.Put, $"/api/movies/{Samples.Id(5)}/poster"));
    }

    [Fact]
    public async Task Empty_fields_are_sent_as_null()
    {
        var app = await OpenAsync("/movies/new");
        Fill(app);

        Submit(app);

        app.WaitForAssertion(() => Assert.Single(Backend.Sent(HttpMethod.Post, "/api/movies")));
        Assert.Equal(
            """{"title":"My film","originalTitle":null,"releaseDate":null,"runtimeMinutes":null,"overview":null,"posterUrl":null,"genreIds":[]}""",
            Backend.Last(HttpMethod.Post, "/api/movies").Body);
    }

    [Fact]
    public async Task Server_side_validation_messages_are_shown()
    {
        Backend.Problem(HttpMethod.Post, "/api/movies", HttpStatusCode.BadRequest, "One or more validation errors occurred.",
            new() { ["title"] = ["The title is taken."] });
        var app = await OpenAsync("/movies/new");
        Fill(app);

        Submit(app);

        app.WaitForAssertion(() => Assert.Contains("The title is taken.", app.Find(".notification.is-danger").TextContent));
        Assert.Equal("/movies/new", CurrentUrl);
        Assert.DoesNotContain("is-loading", app.Find("button[type=submit]").ClassName);
    }

    [Fact]
    public async Task After_adding_it_continues_with_logging_a_viewing()
    {
        var app = await OpenAsync("/movies/new?next=diary");
        Fill(app);
        Assert.Equal("/diary/new", app.Find("a.is-light").GetAttribute("href"));

        Submit(app);

        app.WaitForAssertion(() => Assert.Equal($"/diary/new?movieId={Samples.Id(5)}", CurrentUrl));
    }

    [Fact]
    public async Task After_adding_it_continues_with_the_watchlist()
    {
        Backend.Json(HttpMethod.Put, $"/api/watchlist/{Samples.Id(5)}", Samples.Item(5));
        Backend.Get("/api/watchlist", Samples.WatchlistPage([]));
        Backend.Get("/api/watchlist/tags", Array.Empty<string>());
        var app = await OpenAsync("/movies/new?next=watchlist");
        Fill(app);
        Assert.Equal("/watchlist", app.Find("a.is-light").GetAttribute("href"));

        Submit(app);

        app.WaitForAssertion(() => Assert.Equal("/watchlist", CurrentUrl));
        Assert.Equal("""{"note":null,"tags":[]}""", Backend.Last(HttpMethod.Put, $"/api/watchlist/{Samples.Id(5)}").Body);
    }

    [Fact]
    public async Task A_chosen_image_is_previewed_and_uploaded_after_the_movie_is_saved()
    {
        Backend.Json(HttpMethod.Put, $"/api/movies/{Samples.Id(5)}/poster", Samples.Movie(5, tmdbId: null, posterUrl: $"/api/movies/{Samples.Id(5)}/poster"));
        var app = await OpenAsync("/movies/new");
        Fill(app);

        Upload(app);

        app.WaitForAssertion(() => Assert.StartsWith("data:image/png;base64,", app.Find(".poster-field img").GetAttribute("src")));
        Assert.Equal("poster.png", app.Find(".file-name").TextContent);
        Assert.Equal("Clear the chosen image", app.Find("form button.is-light").TextContent.Trim());

        Submit(app);

        app.WaitForAssertion(() => Assert.Equal($"/movies/{Samples.Id(5)}", CurrentUrl));
        var upload = Backend.Last(HttpMethod.Put, $"/api/movies/{Samples.Id(5)}/poster");
        Assert.Equal("multipart/form-data", upload.ContentType);
        Assert.Contains("filename=poster.png", upload.Body);
        Assert.Contains("Content-Type: image/png", upload.Body);
        // The poster is uploaded after the movie.
        Assert.True(Backend.Requests.ToList().FindIndex(r => r.Method == HttpMethod.Post && r.Path == "/api/movies")
            < Backend.Requests.ToList().FindIndex(r => r.Method == HttpMethod.Put && r.Path.EndsWith("/poster")));
    }

    [Theory]
    [InlineData("poster.gif", "image/gif", 100)]
    [InlineData("poster.svg", "image/svg+xml", 100)]
    [InlineData("big.png", "image/png", 5 * 1024 * 1024 + 1)]
    public async Task A_file_that_is_no_accepted_image_is_refused(string name, string type, int size)
    {
        var app = await OpenAsync("/movies/new");
        Fill(app);

        Upload(app, name, type, new byte[size]);

        app.WaitForAssertion(() => Assert.Equal(
            "Please choose a JPEG, PNG or WebP image of at most 5 MB.", app.Find(".help.is-danger").TextContent));
        Assert.Empty(app.FindAll(".poster-field img"));

        Submit(app);

        Assert.Empty(Backend.Sent(HttpMethod.Post, "/api/movies"));
    }

    [Fact]
    public async Task The_file_field_accepts_the_formats_the_server_accepts()
    {
        var app = await OpenAsync("/movies/new");

        Assert.Equal("image/jpeg,image/png,image/webp", app.Find("#posterFile").GetAttribute("accept"));
    }

    [Fact]
    public async Task The_chosen_image_can_be_cleared()
    {
        var app = await OpenAsync("/movies/new");
        Upload(app);
        app.WaitForAssertion(() => Assert.NotEmpty(app.FindAll(".poster-field img")));

        app.Find("form button.is-light").Click();

        app.WaitForAssertion(() => Assert.Empty(app.FindAll(".poster-field img")));
        Assert.Equal("No file chosen", app.Find(".file-name").TextContent);
        Assert.Empty(app.FindAll("form button.is-light"));
    }

    [Fact]
    public async Task When_the_upload_fails_saving_again_changes_the_movie_that_was_added()
    {
        Backend.Problem(HttpMethod.Put, $"/api/movies/{Samples.Id(5)}/poster", HttpStatusCode.UnsupportedMediaType, "Not an image");
        Backend.Json(HttpMethod.Put, $"/api/movies/{Samples.Id(5)}", Samples.Movie(5, tmdbId: null));
        var app = await OpenAsync("/movies/new");
        Fill(app);
        Upload(app);
        app.WaitForAssertion(() => Assert.NotEmpty(app.FindAll(".poster-field img")));

        Submit(app);
        app.WaitForAssertion(() => Assert.Contains("Not an image", app.Find(".notification.is-danger").TextContent));
        Backend.Json(HttpMethod.Put, $"/api/movies/{Samples.Id(5)}/poster", Samples.Movie(5, tmdbId: null));
        Submit(app);

        app.WaitForAssertion(() => Assert.Equal($"/movies/{Samples.Id(5)}", CurrentUrl));
        Assert.Single(Backend.Sent(HttpMethod.Post, "/api/movies"));
        Assert.Single(Backend.Sent(HttpMethod.Put, $"/api/movies/{Samples.Id(5)}"));
    }

    [Fact]
    public async Task Editing_fills_the_form_with_the_movie()
    {
        Backend.Get($"/api/movies/{Samples.Id(5)}", Samples.Movie(5, tmdbId: null, posterUrl: "https://img.example.com/p.jpg", originalTitle: "Le film"));

        var app = await OpenAsync($"/movies/{Samples.Id(5)}/edit");

        app.WaitForAssertion(() => Assert.Equal("Movie 5", app.Find("#title").GetAttribute("value")));
        Assert.Contains("Edit movie", app.Find("h1").TextContent);
        Assert.Equal("Le film", app.Find("#originalTitle").GetAttribute("value"));
        Assert.Equal("1985-05-17", app.Find("#releaseDate").GetAttribute("value"));
        Assert.Equal("105", app.Find("#runtimeMinutes").GetAttribute("value"));
        Assert.Equal("Overview 5", app.Find("#overview").GetAttribute("value"));
        Assert.Equal("https://img.example.com/p.jpg", app.Find("#posterUrl").GetAttribute("value"));
        app.WaitForAssertion(() => Assert.Equal(["Action", "Drama"], app.FindAll("input[type=checkbox]")
            .Where(c => c.HasAttribute("checked")).Select(c => c.ParentElement!.TextContent.Trim())));
        Assert.Equal($"/movies/{Samples.Id(5)}", app.Find("a.is-light").GetAttribute("href"));
    }

    [Fact]
    public async Task Saving_an_edit_puts_the_movie_and_goes_to_it()
    {
        Backend.Get($"/api/movies/{Samples.Id(5)}", Samples.Movie(5, tmdbId: null));
        Backend.Json(HttpMethod.Put, $"/api/movies/{Samples.Id(5)}", Samples.Movie(5, tmdbId: null));
        var app = await OpenAsync($"/movies/{Samples.Id(5)}/edit");
        app.WaitForAssertion(() => Assert.Equal("Movie 5", app.Find("#title").GetAttribute("value")));

        app.Find("#title").Input("Renamed");
        Submit(app);

        app.WaitForAssertion(() => Assert.Equal($"/movies/{Samples.Id(5)}", CurrentUrl));
        Assert.Contains("\"title\":\"Renamed\"", Backend.Last(HttpMethod.Put, $"/api/movies/{Samples.Id(5)}").Body);
        Assert.Empty(Backend.Sent(HttpMethod.Post, "/api/movies"));
    }

    [Fact]
    public async Task An_uploaded_poster_is_kept_by_sending_its_url_back()
    {
        var posterPath = $"/api/movies/{Samples.Id(5)}/poster";
        Backend.Get($"/api/movies/{Samples.Id(5)}", Samples.Movie(5, tmdbId: null, posterUrl: posterPath));
        Backend.Json(HttpMethod.Put, $"/api/movies/{Samples.Id(5)}", Samples.Movie(5, tmdbId: null));
        var app = await OpenAsync($"/movies/{Samples.Id(5)}/edit");
        app.WaitForAssertion(() => Assert.Equal("Uploaded image", app.Find(".file-name").TextContent));

        // The URL field is not for the path of an uploaded image.
        Assert.Equal("", app.Find("#posterUrl").GetAttribute("value"));
        Assert.Equal(posterPath, app.Find(".poster-field img").GetAttribute("src"));
        Submit(app);

        app.WaitForAssertion(() => Assert.Single(Backend.Sent(HttpMethod.Put, $"/api/movies/{Samples.Id(5)}")));
        Assert.Contains($"\"posterUrl\":\"{posterPath}\"", Backend.Last(HttpMethod.Put, $"/api/movies/{Samples.Id(5)}").Body);
    }

    [Fact]
    public async Task An_uploaded_poster_can_be_removed()
    {
        Backend.Get($"/api/movies/{Samples.Id(5)}", Samples.Movie(5, tmdbId: null, posterUrl: $"/api/movies/{Samples.Id(5)}/poster"));
        Backend.Json(HttpMethod.Put, $"/api/movies/{Samples.Id(5)}", Samples.Movie(5, tmdbId: null));
        var app = await OpenAsync($"/movies/{Samples.Id(5)}/edit");
        app.WaitForAssertion(() => Assert.Equal("Remove the uploaded image", app.Find("form button.is-light").TextContent.Trim()));

        app.Find("form button.is-light").Click();
        Assert.Equal("No file chosen", app.Find(".file-name").TextContent);
        Submit(app);

        app.WaitForAssertion(() => Assert.Single(Backend.Sent(HttpMethod.Put, $"/api/movies/{Samples.Id(5)}")));
        Assert.Contains("\"posterUrl\":null", Backend.Last(HttpMethod.Put, $"/api/movies/{Samples.Id(5)}").Body);
    }

    [Fact]
    public async Task A_typed_url_replaces_an_uploaded_poster()
    {
        Backend.Get($"/api/movies/{Samples.Id(5)}", Samples.Movie(5, tmdbId: null, posterUrl: $"/api/movies/{Samples.Id(5)}/poster"));
        Backend.Json(HttpMethod.Put, $"/api/movies/{Samples.Id(5)}", Samples.Movie(5, tmdbId: null));
        var app = await OpenAsync($"/movies/{Samples.Id(5)}/edit");
        app.WaitForAssertion(() => Assert.Equal("Uploaded image", app.Find(".file-name").TextContent));

        app.Find("#posterUrl").Input("https://img.example.com/new.jpg");
        Submit(app);

        app.WaitForAssertion(() => Assert.Single(Backend.Sent(HttpMethod.Put, $"/api/movies/{Samples.Id(5)}")));
        Assert.Contains("\"posterUrl\":\"https://img.example.com/new.jpg\"", Backend.Last(HttpMethod.Put, $"/api/movies/{Samples.Id(5)}").Body);
    }

    [Fact]
    public async Task A_movie_that_cannot_be_loaded_shows_the_error()
    {
        Backend.Problem(HttpMethod.Get, $"/api/movies/{Samples.Id(5)}", HttpStatusCode.NotFound, "Movie not found");
        await SignInAsync();

        var app = Open($"/movies/{Samples.Id(5)}/edit");

        app.WaitForAssertion(() => Assert.Contains("Movie not found", app.Find(".notification.is-danger").TextContent));
    }
}
