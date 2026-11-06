using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using MovieTracker.Infrastructure.Persistence;

namespace MovieTracker.Api.Auth;

public static class AuthenticationSetup
{
    /// <summary>Where Google sends the user back to. Must be registered as redirect URI in the Google Cloud console.</summary>
    public const string GoogleCallbackPath = "/api/auth/callback/google";

    /// <summary>
    /// Cookie-based sessions; signing in happens via Google OAuth on the server ("backend for frontend"),
    /// so the browser never sees any token. Every endpoint requires a signed-in user unless it says otherwise.
    /// </summary>
    public static IServiceCollection AddMovieTrackerAuthentication(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<UserAccounts>();

        // The keys that encrypt the session cookie live in the database, so users stay signed in when the
        // app is redeployed or runs on a host without a persistent disk (e.g. Render).
        services.AddDataProtection().PersistKeysToDbContext<MovieTrackerDbContext>();

        var authentication = services
            .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.Cookie.Name = "movie_tracker_session";
                options.Cookie.HttpOnly = true;
                // Lax: the cookie is sent on the redirect back from Google, but not on cross-site POSTs.
                options.Cookie.SameSite = SameSiteMode.Lax;
                // Browsers treat http://localhost as secure, so this also works in local development.
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.ExpireTimeSpan = TimeSpan.FromDays(30);
                options.SlidingExpiration = true;

                // An API answers with status codes instead of redirecting to a login page.
                options.Events.OnRedirectToLogin = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                };
                options.Events.OnRedirectToAccessDenied = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return Task.CompletedTask;
                };
            });

        // Without a client ID the Google handler would fail on every request, so it is only
        // registered when configured. The login endpoint then answers with 503.
        var google = configuration.GetSection("Authentication:Google").Get<GoogleSettings>();
        if (!string.IsNullOrWhiteSpace(google?.ClientId) && !string.IsNullOrWhiteSpace(google.ClientSecret))
        {
            authentication.AddGoogle(options =>
            {
                options.ClientId = google.ClientId;
                options.ClientSecret = google.ClientSecret;
                options.CallbackPath = GoogleCallbackPath;
                options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.ClaimActions.MapJsonKey("picture", "picture");
                // The callback is a top-level GET redirect from Google, so Lax is enough (the default is None).
                options.CorrelationCookie.SameSite = SameSiteMode.Lax;
                options.Events.OnCreatingTicket = AddUserIdClaimAsync;
                // E.g. the user cancelled on Google's consent screen: back to the sign-in page instead of an error page.
                options.Events.OnRemoteFailure = context =>
                {
                    context.Response.Redirect("/?loginError=1");
                    context.HandleResponse();
                    return Task.CompletedTask;
                };
            });
        }

        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

        return services;
    }

    private static async Task AddUserIdClaimAsync(Microsoft.AspNetCore.Authentication.OAuth.OAuthCreatingTicketContext context)
    {
        var identity = context.Identity ?? throw new InvalidOperationException("Google sign-in produced no identity.");
        var subject = identity.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? throw new InvalidOperationException("Google did not return an account ID.");
        var email = identity.FindFirst(ClaimTypes.Email)?.Value
            ?? throw new InvalidOperationException("Google did not return an email address.");

        var accounts = context.HttpContext.RequestServices.GetRequiredService<UserAccounts>();
        var user = await accounts.SignInAsync(
            subject,
            email,
            identity.FindFirst(ClaimTypes.Name)?.Value ?? email,
            identity.FindFirst("picture")?.Value,
            context.HttpContext.RequestAborted);

        identity.AddClaim(new Claim(UserClaims.UserId, user.Id.ToString()));
    }

    private sealed record GoogleSettings(string? ClientId, string? ClientSecret);
}
