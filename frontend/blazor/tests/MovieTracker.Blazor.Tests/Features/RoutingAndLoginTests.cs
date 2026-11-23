using MovieTracker.Blazor.Tests.Support;

namespace MovieTracker.Blazor.Tests.Features;

public class RoutingAndLoginTests : AppTest
{
    public RoutingAndLoginTests()
    {
        Backend.Get("/api/movies", Samples.Page<MovieSummary>([], pageSize: 24));
    }

    [Fact]
    public async Task The_sign_in_page_links_to_the_google_sign_in()
    {
        await SignedOutAsync();

        var app = Open("/");

        var link = app.Find("a.button");
        Assert.Equal("/api/auth/login?returnUrl=%2Fmovies", link.GetAttribute("href"));
        // The browser, not Blazor's router, goes to the API.
        Assert.Equal("_top", link.GetAttribute("target"));
        Assert.Contains("Sign in with Google", link.TextContent);
        Assert.Empty(app.FindAll("nav.navbar"));
    }

    [Fact]
    public async Task The_sign_in_page_sends_the_user_back_to_where_they_wanted_to_go()
    {
        await SignedOutAsync();

        var app = Open("/?returnUrl=%2Fdiary%3Fpage%3D2");

        Assert.Equal("/api/auth/login?returnUrl=%2Fdiary%3Fpage%3D2", app.Find("a.button").GetAttribute("href"));
    }

    [Fact]
    public async Task The_sign_in_page_ignores_a_return_url_to_another_site()
    {
        await SignedOutAsync();

        var app = Open("/?returnUrl=%2F%2Fevil.example.com");

        Assert.Equal("/api/auth/login?returnUrl=%2Fmovies", app.Find("a.button").GetAttribute("href"));
    }

    [Fact]
    public async Task The_sign_in_page_says_when_signing_in_failed()
    {
        await SignedOutAsync();

        var app = Open("/?loginError=access_denied");

        Assert.Contains("Signing in did not work or was cancelled", app.Find(".notification").TextContent);
    }

    [Fact]
    public async Task The_sign_in_page_is_not_for_signed_in_users()
    {
        await SignInAsync();

        Open("/");

        Assert.Equal("/movies", CurrentUrl);
    }

    [Fact]
    public async Task Signed_in_users_go_on_to_the_return_url()
    {
        await SignInAsync();
        Backend.Get("/api/diary-entries", Samples.Page<DiaryEntry>([]));

        Open("/?returnUrl=%2Fdiary%3Fpage%3D2");

        Assert.Equal("/diary?page=2", CurrentUrl);
    }

    [Fact]
    public async Task Signed_in_users_do_not_go_on_to_another_site()
    {
        await SignInAsync();

        Open("/?returnUrl=https%3A%2F%2Fevil.example.com");

        Assert.Equal("/movies", CurrentUrl);
    }

    [Theory]
    [InlineData("/movies")]
    [InlineData("/diary/new")]
    [InlineData("/watchlist")]
    [InlineData("/activity")]
    [InlineData("/import")]
    public async Task Every_page_but_the_sign_in_page_needs_a_session(string url)
    {
        await SignedOutAsync();

        var app = Open(url);

        Assert.Equal($"/?returnUrl={Uri.EscapeDataString(url)}", CurrentUrl);
        Assert.Contains("Sign in with Google", app.Markup);
    }

    [Fact]
    public async Task The_page_to_come_back_to_includes_its_query()
    {
        await SignedOutAsync();

        Open("/diary?tag=cinema&page=2");

        Assert.Equal("/?returnUrl=%2Fdiary%3Ftag%3Dcinema%26page%3D2", CurrentUrl);
    }

    [Fact]
    public async Task Unknown_urls_lead_to_the_start_page()
    {
        await SignInAsync();

        Open("/no/such/page");

        Assert.Equal("/movies", CurrentUrl);
    }

    [Fact]
    public async Task Unknown_urls_lead_to_the_sign_in_page_without_a_session()
    {
        await SignedOutAsync();

        var app = Open("/no/such/page");

        Assert.Equal("/", CurrentUrl);
        Assert.Contains("Sign in with Google", app.Markup);
    }

    [Fact]
    public async Task The_navigation_shows_the_user_and_marks_the_current_section()
    {
        await SignInAsync();
        Backend.Get("/api/diary-entries", Samples.Page<DiaryEntry>([]));

        var app = Open("/diary");

        Assert.Contains("Jane Doe", app.Find(".navbar-end").TextContent);
        Assert.Equal(["Diary"], app.FindAll(".navbar-start .is-selected").Select(a => a.TextContent.Trim()));
        Assert.Contains("TMDB", app.Find("footer").TextContent);
    }

    [Fact]
    public async Task A_section_is_current_on_the_pages_below_it()
    {
        await SignInAsync();
        Backend.Get("/api/diary-entries", Samples.Page<DiaryEntry>([]));

        var app = Open("/diary/new");

        Assert.Equal(["Diary"], app.FindAll(".navbar-start .is-selected").Select(a => a.TextContent.Trim()));
    }

    [Fact]
    public async Task The_menu_button_opens_the_menu_on_small_screens()
    {
        await SignInAsync();
        var app = Open("/movies");
        Assert.Empty(app.FindAll(".navbar-menu.is-active"));

        app.Find(".navbar-burger").Click();

        Assert.Single(app.FindAll(".navbar-menu.is-active"));
        Assert.Equal("true", app.Find(".navbar-burger").GetAttribute("aria-expanded"));
    }

    [Fact]
    public async Task Signing_out_leads_to_the_sign_in_page()
    {
        await SignInAsync();
        Backend.NoContent(HttpMethod.Post, "/api/auth/logout");
        var app = Open("/movies");

        app.Find(".navbar-end button").Click();

        app.WaitForAssertion(() => Assert.Contains("Sign in with Google", app.Markup));
        Assert.Equal("/", CurrentUrl);
        Assert.Empty(app.FindAll("nav.navbar"));
    }

    [Fact]
    public async Task An_expired_session_leads_to_the_sign_in_page_and_back_afterwards()
    {
        await SignInAsync();
        Backend.Problem(HttpMethod.Get, "/api/movies", System.Net.HttpStatusCode.Unauthorized, "Expired");

        var app = Open("/movies?page=3");

        app.WaitForAssertion(() => Assert.Contains("Sign in with Google", app.Markup));
        Assert.Equal("/?returnUrl=%2Fmovies%3Fpage%3D3", CurrentUrl);
        Assert.Equal("/api/auth/login?returnUrl=%2Fmovies%3Fpage%3D3", app.Find("a.button").GetAttribute("href"));
    }

    [Fact]
    public async Task Several_requests_failing_at_once_remember_the_page_only_once()
    {
        await SignInAsync();
        Backend.Problem(HttpMethod.Get, "/api/watchlist", System.Net.HttpStatusCode.Unauthorized, "Expired");
        Backend.Problem(HttpMethod.Get, "/api/watchlist/tags", System.Net.HttpStatusCode.Unauthorized, "Expired");
        Backend.Problem(HttpMethod.Get, "/api/genres", System.Net.HttpStatusCode.Unauthorized, "Expired");

        var app = Open("/watchlist");

        app.WaitForAssertion(() => Assert.Contains("Sign in with Google", app.Markup));
        Assert.Equal("/?returnUrl=%2Fwatchlist", CurrentUrl);
    }

    [Fact]
    public async Task A_page_loads_its_data_once()
    {
        await SignInAsync();

        Open("/movies?page=2");

        Assert.Single(Backend.Sent(HttpMethod.Get, "/api/movies"));
    }
}
