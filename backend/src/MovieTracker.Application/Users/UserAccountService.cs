using MovieTracker.Domain.Users;

namespace MovieTracker.Application.Users;

/// <summary>Creates the user on their first sign-in and keeps the profile in sync with Google afterwards.</summary>
public sealed class UserAccountService(IUserRepository users, IUnitOfWork unitOfWork, TimeProvider timeProvider)
{
    public async Task<User> SignInAsync(
        string googleSubject, string email, string name, string? pictureUrl, CancellationToken cancellationToken)
    {
        var user = await users.FindByGoogleSubjectAsync(googleSubject, cancellationToken);
        if (user is null)
        {
            user = new User(googleSubject, email, name, pictureUrl, timeProvider.GetUtcNow());
            users.Add(user);
        }
        else
        {
            user.UpdateProfile(email, name, pictureUrl);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return user;
    }

    /// <summary>Null if the user does not exist (any more).</summary>
    public Task<User?> FindAsync(Guid userId, CancellationToken cancellationToken) =>
        users.FindByIdAsync(userId, cancellationToken);
}
