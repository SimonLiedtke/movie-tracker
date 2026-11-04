using System.Net.Mime;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MovieTracker.Api.Auth;
using MovieTracker.Api.Contracts;
using MovieTracker.Infrastructure.Persistence;

namespace MovieTracker.Api.Controllers;

[ApiController]
[Route("api/auth")]
[Tags("Auth")]
public sealed class AuthController(MovieTrackerDbContext db, IAuthenticationSchemeProvider schemes) : ControllerBase
{
    /// <summary>
    /// Starts the Google sign-in. The browser navigates here (it is not an XHR endpoint); after signing in,
    /// Google redirects back to the API, which sets the session cookie and redirects to <paramref name="returnUrl"/>.
    /// </summary>
    [HttpGet("login", Name = "login")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status302Found)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable, MediaTypeNames.Application.ProblemJson)]
    public async Task<IActionResult> Login([FromQuery] string? returnUrl)
    {
        if (await schemes.GetSchemeAsync(GoogleDefaults.AuthenticationScheme) is null)
        {
            return Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Google sign-in is not configured",
                detail: "Set Authentication:Google:ClientId and Authentication:Google:ClientSecret.");
        }

        // Only local URLs: otherwise the login link could be abused to redirect to a phishing site.
        var redirectUri = Url.IsLocalUrl(returnUrl) ? returnUrl : "/";
        return Challenge(new AuthenticationProperties { RedirectUri = redirectUri }, GoogleDefaults.AuthenticationScheme);
    }

    [HttpPost("logout", Name = "logout")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return NoContent();
    }

    /// <summary>The signed-in user, or 401 if nobody is signed in.</summary>
    [HttpGet("me", Name = "getCurrentUser")]
    [ProducesResponseType<UserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, MediaTypeNames.Application.ProblemJson)]
    public async Task<ActionResult<UserResponse>> GetCurrentUser(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var user = await db.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new UserResponse(u.Id, u.Email, u.Name, u.PictureUrl))
            .SingleOrDefaultAsync(cancellationToken);

        if (user is null)
        {
            // The session outlived the account: end it.
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Unauthorized();
        }

        return user;
    }
}
