using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MovieTracker.Api.Auth;
using MovieTracker.Api.Contracts;
using MovieTracker.Api.Tests.Infrastructure;

namespace MovieTracker.Api.Tests;

public sealed class AuthTests(ApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task Me_returns_the_signed_in_user()
    {
        var me = await GetAsync<UserResponse>("/api/auth/me");

        Assert.Equal(("alice@example.com", "alice", (string?)null), (me.Email, me.Name, me.PictureUrl));
    }

    [Fact]
    public async Task Me_returns_401_when_nobody_is_signed_in()
    {
        var response = await Anonymous.GetAsync("/api/auth/me", Ct);

        await ReadProblemAsync(response, HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("GET", "/api/genres")]
    [InlineData("GET", "/api/movies")]
    [InlineData("POST", "/api/movies")]
    [InlineData("GET", "/api/movies/01a19ec6-1b6d-7ad1-bf1a-bd69fd2b73be")]
    [InlineData("PUT", "/api/movies/01a19ec6-1b6d-7ad1-bf1a-bd69fd2b73be")]
    [InlineData("DELETE", "/api/movies/01a19ec6-1b6d-7ad1-bf1a-bd69fd2b73be")]
    [InlineData("GET", "/api/movies/01a19ec6-1b6d-7ad1-bf1a-bd69fd2b73be/diary-entries")]
    [InlineData("GET", "/api/diary-entries")]
    [InlineData("POST", "/api/diary-entries")]
    [InlineData("GET", "/api/diary-entries/01a19ec6-1b6d-7ad1-bf1a-bd69fd2b73be")]
    [InlineData("PUT", "/api/diary-entries/01a19ec6-1b6d-7ad1-bf1a-bd69fd2b73be")]
    [InlineData("DELETE", "/api/diary-entries/01a19ec6-1b6d-7ad1-bf1a-bd69fd2b73be")]
    [InlineData("GET", "/api/watchlist")]
    [InlineData("PUT", "/api/watchlist/01a19ec6-1b6d-7ad1-bf1a-bd69fd2b73be")]
    [InlineData("DELETE", "/api/watchlist/01a19ec6-1b6d-7ad1-bf1a-bd69fd2b73be")]
    public async Task Every_endpoint_requires_a_signed_in_user(string method, string url)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), url);
        if (method is "POST" or "PUT")
        {
            request.Content = JsonContent.Create(new { });
        }

        var response = await Anonymous.SendAsync(request, Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_redirects_to_google_and_back_to_the_api()
    {
        using var client = CreateClient(Factory, user: null, allowAutoRedirect: false);

        var response = await client.GetAsync("/api/auth/login?returnUrl=%2Fdiary%3Fpage%3D2", Ct);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location!;
        Assert.Equal("accounts.google.com", location.Host);
        var query = QueryHelpers.ParseQuery(location.Query);
        Assert.Equal("test-client-id", query["client_id"]);
        Assert.Equal("http://localhost/api/auth/callback/google", query["redirect_uri"]);
        Assert.Equal("/diary?page=2", ReturnUrlFromState(query["state"].ToString()));
    }

    [Theory]
    [InlineData("https://evil.example.com/")]
    [InlineData("//evil.example.com/")]
    [InlineData(null)]
    public async Task Login_only_returns_to_local_urls(string? returnUrl)
    {
        using var client = CreateClient(Factory, user: null, allowAutoRedirect: false);
        var url = returnUrl is null ? "/api/auth/login" : $"/api/auth/login?returnUrl={Uri.EscapeDataString(returnUrl)}";

        var response = await client.GetAsync(url, Ct);

        var state = QueryHelpers.ParseQuery(response.Headers.Location!.Query)["state"].ToString();
        Assert.Equal("/", ReturnUrlFromState(state));
    }

    [Fact]
    public async Task Login_returns_503_when_google_is_not_configured()
    {
        using var unconfigured = Factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Authentication:Google:ClientId", "");
            builder.UseSetting("Authentication:Google:ClientSecret", "");
        });
        using var client = unconfigured.CreateClient(new() { AllowAutoRedirect = false });

        var response = await client.GetAsync("/api/auth/login", Ct);

        var problem = await ReadProblemAsync(response, HttpStatusCode.ServiceUnavailable);
        Assert.Equal("Google sign-in is not configured", problem.Title);
    }

    [Fact]
    public async Task Logout_deletes_the_session_cookie()
    {
        var response = await Client.PostAsync("/api/auth/logout", content: null, Ct);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var setCookie = Assert.Single(response.Headers.GetValues("Set-Cookie"));
        Assert.StartsWith("movie_tracker_session=;", setCookie);
        Assert.Contains("expires=Thu, 01 Jan 1970", setCookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task State_changing_requests_require_the_csrf_header()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/movies")
        {
            Content = JsonContent.Create(new { title = "Alien", genreIds = Array.Empty<int>() }),
        };
        request.Headers.Add(TestAuthHandler.UserHeader, "alice");
        using var client = Factory.CreateClient(); // without the default CSRF header

        var response = await client.SendAsync(request, Ct);

        var problem = await ReadProblemAsync(response, HttpStatusCode.BadRequest);
        Assert.Equal("Missing CSRF header", problem.Title);
        Assert.Empty((await GetAsync<PagedResponse<MovieSummaryResponse>>("/api/movies")).Items);
    }

    [Fact]
    public async Task Signing_in_again_updates_the_profile_of_the_same_user()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var accounts = scope.ServiceProvider.GetRequiredService<UserAccounts>();

        var first = await accounts.SignInAsync("google-42", "ada@example.com", "Ada", null, Ct);
        var second = await accounts.SignInAsync("google-42", "countess@example.com", "Countess", "https://example.com/c.png", Ct);

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(("countess@example.com", "Countess"), (second.Email, second.Name));
    }

    private string? ReturnUrlFromState(string state)
    {
        var options = Factory.Services.GetRequiredService<IOptionsMonitor<GoogleOptions>>().Get(GoogleDefaults.AuthenticationScheme);
        return options.StateDataFormat.Unprotect(state)?.RedirectUri;
    }
}
