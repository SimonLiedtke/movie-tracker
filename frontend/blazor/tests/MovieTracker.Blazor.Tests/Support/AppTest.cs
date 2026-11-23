using Microsoft.AspNetCore.Components;
using MovieTracker.Blazor.Tests.Support;

namespace MovieTracker.Blazor.Tests.Support;

/// <summary>
/// Renders the app the way it runs: the router with all pages, against a fake backend. So the tests cover
/// routing, the guards, the components and the API calls together.
/// </summary>
public abstract class AppTest : BunitContext
{
    protected AppTest()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMovieTracker(new Uri("http://localhost/"), () => Backend);
        Backend.Get("/api/genres", Samples.Genres);
    }

    protected FakeBackend Backend { get; } = new();

    protected NavigationManager Navigation => Services.GetRequiredService<NavigationManager>();

    protected AuthService Auth => Services.GetRequiredService<AuthService>();

    /// <summary>The path and query the app is on now, e.g. "/diary?page=2".</summary>
    protected string CurrentUrl => Auth.CurrentUrl();

    /// <summary>Someone is signed in when the app starts.</summary>
    protected async Task SignInAsync()
    {
        Backend.Get("/api/auth/me", Samples.Jane);
        await Auth.LoadAsync();
    }

    /// <summary>Nobody is signed in when the app starts.</summary>
    protected async Task SignedOutAsync()
    {
        Backend.Problem(HttpMethod.Get, "/api/auth/me", System.Net.HttpStatusCode.Unauthorized, "Not signed in");
        await Auth.LoadAsync();
    }

    /// <summary>Starts the app on the page; the user must have been signed in or out before.</summary>
    protected IRenderedComponent<App> Open(string url)
    {
        Navigation.NavigateTo(url);
        return Render<App>();
    }

    /// <summary>Makes the browser's confirm() dialog answer.</summary>
    protected void AnswerConfirm(bool answer) => JSInterop.Setup<bool>("confirm", _ => true).SetResult(answer);
}
