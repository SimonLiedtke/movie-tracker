using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MovieTracker.Api.Auth;

namespace MovieTracker.Api.Tests.Infrastructure;

/// <summary>
/// Replaces the Google sign-in in tests: a request is signed in as the user named in the
/// <see cref="UserHeader"/> header. The user is created through the same <see cref="UserAccounts"/>
/// as a real Google sign-in, so the claims match production.
/// </summary>
public sealed class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder, UserAccounts accounts)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";
    public const string UserHeader = "X-Test-User";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var name = Request.Headers[UserHeader].ToString();
        if (string.IsNullOrEmpty(name))
        {
            return AuthenticateResult.NoResult();
        }

        var user = await accounts.SignInAsync(
            $"test-subject-{name}", $"{name}@example.com", name, pictureUrl: null, Context.RequestAborted);

        var identity = new ClaimsIdentity(
            [new Claim(UserClaims.UserId, user.Id.ToString()), new Claim(ClaimTypes.Name, user.Name)],
            SchemeName);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName));
    }
}
