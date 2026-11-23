using System.Net;
using MovieTracker.Blazor.Tests.Support;

namespace MovieTracker.Blazor.Tests.Core;

public class AuthTests : AppTest
{
    [Fact]
    public async Task The_signed_in_user_is_loaded()
    {
        await SignInAsync();

        Assert.True(Auth.SignedIn);
        Assert.Equal("Jane Doe", Auth.User!.Name);
    }

    [Fact]
    public async Task Nobody_is_signed_in_when_the_API_answers_401()
    {
        await SignedOutAsync();

        Assert.False(Auth.SignedIn);
        Assert.Null(Auth.User);
    }

    [Fact]
    public async Task Nobody_is_signed_in_when_the_API_cannot_be_asked()
    {
        Backend.On(HttpMethod.Get, "/api/auth/me", _ => throw new HttpRequestException("down"));

        await Auth.LoadAsync();

        Assert.False(Auth.SignedIn);
    }

    [Fact]
    public async Task A_401_of_the_auth_endpoints_does_not_end_the_session_again()
    {
        await SignedOutAsync();
        Navigation.NavigateTo("/diary");

        await Auth.LoadAsync();

        Assert.Equal("/diary", CurrentUrl);
    }

    [Fact]
    public async Task Requests_to_the_API_carry_the_CSRF_header()
    {
        await SignInAsync();
        Backend.Get("/api/genres", Samples.Genres);
        var api = Services.GetRequiredService<Api>();

        await api.Genres.ListAsync();

        Assert.All(Backend.Requests, request => Assert.True(request.HasCsrfHeader, request.Path));
    }

    [Fact]
    public async Task A_401_of_another_endpoint_ends_the_session_and_remembers_the_page()
    {
        await SignInAsync();
        Backend.Problem(HttpMethod.Get, "/api/movies", HttpStatusCode.Unauthorized, "Expired");
        Navigation.NavigateTo("/movies?page=2");

        await Assert.ThrowsAsync<ApiException>(() => Services.GetRequiredService<Api>().Movies.ListAsync());

        Assert.False(Auth.SignedIn);
        Assert.Equal("/?returnUrl=%2Fmovies%3Fpage%3D2", CurrentUrl);
    }

    [Fact]
    public async Task Signing_out_posts_to_the_API_and_goes_to_the_sign_in_page()
    {
        await SignInAsync();
        Backend.NoContent(HttpMethod.Post, "/api/auth/logout");
        Navigation.NavigateTo("/diary");

        await Auth.LogoutAsync();

        Assert.Single(Backend.Sent(HttpMethod.Post, "/api/auth/logout"));
        Assert.False(Auth.SignedIn);
        Assert.Equal("/", CurrentUrl);
    }

    [Fact]
    public async Task The_session_ends_even_if_signing_out_fails()
    {
        await SignInAsync();
        Backend.Problem(HttpMethod.Post, "/api/auth/logout", HttpStatusCode.InternalServerError, "Broken");

        await Assert.ThrowsAsync<ApiException>(() => Auth.LogoutAsync());

        Assert.False(Auth.SignedIn);
    }

    [Theory]
    [InlineData("/diary?page=2", true)]
    [InlineData("/", true)]
    [InlineData("//example.com", false)]
    [InlineData("/\\example.com", false)]
    [InlineData("https://example.com", false)]
    [InlineData("diary", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Only_paths_within_the_app_are_local(string? url, bool expected)
    {
        Assert.Equal(expected, AuthService.IsLocalPath(url));
    }

    [Fact]
    public void The_login_url_asks_for_the_movies_by_default()
    {
        Assert.Equal("/api/auth/login?returnUrl=%2Fmovies", AuthService.LoginUrl());
        Assert.Equal("/api/auth/login?returnUrl=%2Fdiary%3Fpage%3D2", AuthService.LoginUrl("/diary?page=2"));
    }
}
