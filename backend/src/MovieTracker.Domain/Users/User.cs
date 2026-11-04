namespace MovieTracker.Domain.Users;

/// <summary>
/// A person who signs in with their Google account. Diary entries and watchlist items belong to a user.
/// </summary>
public sealed class User
{
    private User()
    {
        // Required by EF Core.
    }

    public User(string googleSubject, string email, string name, string? pictureUrl, DateTimeOffset createdAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(googleSubject);

        Id = Guid.CreateVersion7(createdAt);
        GoogleSubject = googleSubject;
        CreatedAt = createdAt;
        UpdateProfile(email, name, pictureUrl);
    }

    public Guid Id { get; private set; }

    /// <summary>Google's stable account ID (the "sub" claim). Email addresses can change, this cannot.</summary>
    public string GoogleSubject { get; private set; } = null!;

    public string Email { get; private set; } = null!;

    public string Name { get; private set; } = null!;

    public string? PictureUrl { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Takes over the latest profile data from Google, called on every sign-in.</summary>
    public void UpdateProfile(string email, string name, string? pictureUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        Email = email;
        // Google accounts can lack a display name; fall back to the email address.
        Name = string.IsNullOrWhiteSpace(name) ? email : name.Trim();
        PictureUrl = pictureUrl;
    }
}
