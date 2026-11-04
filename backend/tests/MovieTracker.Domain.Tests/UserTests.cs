using MovieTracker.Domain.Users;

namespace MovieTracker.Domain.Tests;

public sealed class UserTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Creates_a_user_from_the_google_profile()
    {
        var user = new User("google-123", "ada@example.com", " Ada Lovelace ", "https://example.com/ada.png", CreatedAt);

        Assert.Equal(7, user.Id.Version);
        Assert.Equal("google-123", user.GoogleSubject);
        Assert.Equal("ada@example.com", user.Email);
        Assert.Equal("Ada Lovelace", user.Name);
        Assert.Equal("https://example.com/ada.png", user.PictureUrl);
        Assert.Equal(CreatedAt, user.CreatedAt);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Uses_the_email_address_when_there_is_no_name(string name)
    {
        var user = new User("google-123", "ada@example.com", name, null, CreatedAt);

        Assert.Equal("ada@example.com", user.Name);
    }

    [Fact]
    public void Requires_a_google_account_id_and_an_email_address()
    {
        Assert.Throws<ArgumentException>(() => new User(" ", "ada@example.com", "Ada", null, CreatedAt));
        Assert.Throws<ArgumentException>(() => new User("google-123", "", "Ada", null, CreatedAt));
    }

    [Fact]
    public void UpdateProfile_replaces_the_profile_but_not_the_account_id()
    {
        var user = new User("google-123", "ada@example.com", "Ada", "https://example.com/old.png", CreatedAt);

        user.UpdateProfile("countess@example.com", "Countess of Lovelace", null);

        Assert.Equal("google-123", user.GoogleSubject);
        Assert.Equal(("countess@example.com", "Countess of Lovelace", (string?)null), (user.Email, user.Name, user.PictureUrl));
    }
}
