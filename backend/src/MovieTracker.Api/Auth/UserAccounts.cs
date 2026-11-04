using Microsoft.EntityFrameworkCore;
using MovieTracker.Domain.Users;
using MovieTracker.Infrastructure.Persistence;

namespace MovieTracker.Api.Auth;

/// <summary>Creates the user on their first sign-in and keeps the profile in sync with Google afterwards.</summary>
public sealed class UserAccounts(MovieTrackerDbContext db, TimeProvider timeProvider)
{
    public async Task<User> SignInAsync(
        string googleSubject, string email, string name, string? pictureUrl, CancellationToken cancellationToken)
    {
        var user = await db.Users.SingleOrDefaultAsync(u => u.GoogleSubject == googleSubject, cancellationToken);
        if (user is null)
        {
            user = new User(googleSubject, email, name, pictureUrl, timeProvider.GetUtcNow());
            db.Users.Add(user);
        }
        else
        {
            user.UpdateProfile(email, name, pictureUrl);
        }

        await db.SaveChangesAsync(cancellationToken);
        return user;
    }
}
