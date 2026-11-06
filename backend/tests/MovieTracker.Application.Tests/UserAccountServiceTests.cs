using MovieTracker.Application.Tests.Fakes;
using MovieTracker.Application.Users;

namespace MovieTracker.Application.Tests;

public sealed class UserAccountServiceTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeUserRepository _users = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly TestClock _clock = new(Start);
    private readonly UserAccountService _service;

    public UserAccountServiceTests()
    {
        _service = new UserAccountService(_users, _unitOfWork, _clock);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_first_sign_in_creates_the_user_and_saves_it()
    {
        var user = await _service.SignInAsync("google-1", "ada@example.com", "Ada", "https://example.com/ada.png", Ct);

        Assert.Same(user, Assert.Single(_users.Users));
        Assert.Equal(("google-1", "ada@example.com", "Ada", "https://example.com/ada.png"), (user.GoogleSubject, user.Email, user.Name, user.PictureUrl));
        Assert.Equal(Start, user.CreatedAt);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Signing_in_again_updates_the_profile_of_the_same_user()
    {
        var first = await _service.SignInAsync("google-1", "ada@example.com", "Ada", null, Ct);
        _clock.Advance(TimeSpan.FromDays(1));

        var second = await _service.SignInAsync("google-1", "countess@example.com", "Countess", "https://example.com/c.png", Ct);

        Assert.Same(first, second);
        Assert.Single(_users.Users);
        Assert.Equal(("countess@example.com", "Countess", "https://example.com/c.png"), (second.Email, second.Name, second.PictureUrl));
        // The account stays as old as it is.
        Assert.Equal(Start, second.CreatedAt);
        Assert.Equal(2, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Users_are_told_apart_by_their_google_account_not_by_their_email()
    {
        var first = await _service.SignInAsync("google-1", "shared@example.com", "Ada", null, Ct);
        var second = await _service.SignInAsync("google-2", "shared@example.com", "Bob", null, Ct);

        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(2, _users.Users.Count);
    }

    [Fact]
    public async Task A_google_account_without_a_name_is_called_by_its_email_address()
    {
        var user = await _service.SignInAsync("google-1", "ada@example.com", " ", null, Ct);

        Assert.Equal("ada@example.com", user.Name);
    }

    [Fact]
    public async Task Finds_a_user_by_ID()
    {
        var user = await _service.SignInAsync("google-1", "ada@example.com", "Ada", null, Ct);

        Assert.Same(user, await _service.FindAsync(user.Id, Ct));
    }

    [Fact]
    public async Task Finds_nobody_for_an_unknown_ID()
    {
        await _service.SignInAsync("google-1", "ada@example.com", "Ada", null, Ct);

        Assert.Null(await _service.FindAsync(Guid.NewGuid(), Ct));
    }
}
