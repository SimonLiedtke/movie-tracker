using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace MovieTracker.Blazor.Core.Auth;

/// <summary>
/// The signed-in user. Signing in happens on the backend (Google OAuth), which sets an HttpOnly
/// session cookie; the frontend never handles tokens, it only asks the API who is signed in.
/// </summary>
public sealed class AuthService(ApiClient client, NavigationManager navigation, ILogger<AuthService> logger)
{
    public User? User { get; private set; }

    public bool SignedIn => User is not null;

    /// <summary>Raised when someone signs in or the session has ended.</summary>
    public event Action? Changed;

    /// <summary>Asks the API for the signed-in user. Called once on app start.</summary>
    public async Task LoadAsync()
    {
        try
        {
            User = await client.GetAsync("/api/auth/me", ApiJson.Default.User);
        }
        catch (Exception exception)
        {
            if (exception is not ApiException { Error.Status: 401 })
            {
                logger.LogError(exception, "Could not load the signed-in user");
            }

            User = null;
        }

        Changed?.Invoke();
    }

    /// <summary>The URL that starts the Google sign-in; the browser navigates there (no XHR).</summary>
    public static string LoginUrl(string? returnUrl = null) =>
        $"/api/auth/login?returnUrl={Uri.EscapeDataString(returnUrl ?? "/movies")}";

    public async Task LogoutAsync()
    {
        try
        {
            await client.SendAsync(HttpMethod.Post, "/api/auth/logout");
        }
        finally
        {
            SessionEnded();
        }
    }

    /// <summary>The session is gone (signed out or expired): back to the sign-in page.</summary>
    public void SessionEnded(string? returnUrl = null)
    {
        User = null;
        Changed?.Invoke();
        navigation.NavigateTo(SignInPath(returnUrl));
    }

    /// <summary>The sign-in page, which sends the user on to <paramref name="returnUrl"/> after signing in.</summary>
    public static string SignInPath(string? returnUrl) =>
        string.IsNullOrEmpty(returnUrl) ? "/" : $"/?returnUrl={Uri.EscapeDataString(returnUrl)}";

    /// <summary>The page the user is on, e.g. "/diary?page=2".</summary>
    public string CurrentUrl() => "/" + navigation.ToBaseRelativePath(navigation.Uri);

    /// <summary>A path within this app, e.g. "/diary?page=2": never another site, as in "//example.com".</summary>
    public static bool IsLocalPath(string? url) =>
        !string.IsNullOrEmpty(url)
        && url.StartsWith('/')
        && !url.StartsWith("//", StringComparison.Ordinal)
        && !url.StartsWith("/\\", StringComparison.Ordinal);
}

/// <summary>Marks a page that does not require a session (the sign-in page).</summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class PublicPageAttribute : Attribute;
