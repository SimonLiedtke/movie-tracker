using MovieTracker.Domain.Activity;

namespace MovieTracker.Domain.Tests;

public sealed class ActivityLogEntryTests
{
    private static readonly Guid User = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void An_action_on_a_movie_keeps_the_movie_and_when_it_happened()
    {
        var movieId = Guid.NewGuid();

        var entry = ActivityLogEntry.ForMovie(User, ActivityType.WatchlistItemAdded, Now, movieId, "Alien", "Note: tonight");

        Assert.Equal(
            (User, ActivityType.WatchlistItemAdded, Now, movieId, "Alien", "Note: tonight"),
            (entry.UserId, entry.Type, entry.OccurredAt, entry.MovieId, entry.MovieTitle, entry.Detail));
        Assert.NotEqual(Guid.Empty, entry.Id);
    }

    [Fact]
    public void An_action_that_is_not_about_a_movie_has_none()
    {
        var entry = ActivityLogEntry.General(User, ActivityType.LetterboxdImported, Now);

        Assert.Equal((null, null, null), (entry.MovieId, entry.MovieTitle, entry.Detail));
    }

    [Fact]
    public void A_movie_that_is_deleted_is_named_but_not_referenced()
    {
        var entry = ActivityLogEntry.ForMovie(User, ActivityType.MovieDeleted, Now, movieId: null, "Home Video");

        Assert.Equal((null, "Home Video"), (entry.MovieId, entry.MovieTitle));
    }

    [Fact]
    public void Too_long_texts_are_shortened_to_fit_the_columns()
    {
        var entry = ActivityLogEntry.ForMovie(
            User,
            ActivityType.WatchlistItemAdded,
            Now,
            Guid.NewGuid(),
            new string('t', ActivityLogEntry.MaxMovieTitleLength + 50),
            new string('d', ActivityLogEntry.MaxDetailLength + 50));

        Assert.Equal(ActivityLogEntry.MaxMovieTitleLength, entry.MovieTitle!.Length);
        Assert.Equal(ActivityLogEntry.MaxDetailLength, entry.Detail!.Length);
        Assert.EndsWith("…", entry.Detail);
    }

    [Fact]
    public void An_action_needs_a_user_and_a_movie_needs_a_title()
    {
        Assert.Throws<ArgumentException>(() => ActivityLogEntry.General(Guid.Empty, ActivityType.LetterboxdImported, Now));
        Assert.Throws<ArgumentException>(() => ActivityLogEntry.ForMovie(User, ActivityType.MovieAdded, Now, Guid.NewGuid(), " "));
    }
}
