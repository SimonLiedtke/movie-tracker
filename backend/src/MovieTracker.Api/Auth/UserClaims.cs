using System.Security.Claims;

namespace MovieTracker.Api.Auth;

public static class UserClaims
{
    /// <summary>Claim with our own user ID (not Google's), added when the user signs in.</summary>
    public const string UserId = "movie_tracker_user_id";

    public static Guid? FindUserId(this ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(UserId), out var userId) ? userId : null;

    /// <summary>The signed-in user's ID. Only for endpoints that require authentication.</summary>
    public static Guid GetUserId(this ClaimsPrincipal principal) =>
        principal.FindUserId() ?? throw new InvalidOperationException("The request has no signed-in user.");
}
