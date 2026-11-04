using MovieTracker.Domain.Diary;

namespace MovieTracker.Domain.Tests;

public sealed class DiaryEntryTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Creates_an_entry_from_its_details()
    {
        var movieId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var entry = new DiaryEntry(
            userId,
            new DiaryEntryDetails(movieId, new DateOnly(2026, 10, 31), new Rating(9), "Still terrifying.", IsRewatch: true, Liked: true),
            CreatedAt);

        Assert.Equal(7, entry.Id.Version);
        Assert.Equal(userId, entry.UserId);
        Assert.Equal(movieId, entry.MovieId);
        Assert.Equal(new DateOnly(2026, 10, 31), entry.WatchedOn);
        Assert.Equal(new Rating(9), entry.Rating);
        Assert.Equal("Still terrifying.", entry.Review);
        Assert.True(entry.IsRewatch);
        Assert.True(entry.Liked);
        Assert.Equal(CreatedAt, entry.CreatedAt);
    }

    [Fact]
    public void Update_replaces_all_details_but_keeps_the_owner()
    {
        var userId = Guid.NewGuid();
        var entry = new DiaryEntry(
            userId,
            new DiaryEntryDetails(Guid.NewGuid(), new DateOnly(2026, 10, 31), new Rating(9), "Great", true, true),
            CreatedAt);
        var otherMovieId = Guid.NewGuid();

        entry.Update(new DiaryEntryDetails(otherMovieId, new DateOnly(2026, 11, 1), null, null, false, false));

        Assert.Equal(userId, entry.UserId);
        Assert.Equal(otherMovieId, entry.MovieId);
        Assert.Equal(new DateOnly(2026, 11, 1), entry.WatchedOn);
        Assert.Null(entry.Rating);
        Assert.Null(entry.Review);
        Assert.False(entry.IsRewatch);
        Assert.False(entry.Liked);
        Assert.Equal(CreatedAt, entry.CreatedAt);
    }
}
