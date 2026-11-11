using MovieTracker.Domain.Watchlist;

namespace MovieTracker.Domain.Tests;

public sealed class WatchlistItemTests
{
    private static readonly DateTimeOffset AddedAt = new(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Stores_the_tags_normalized()
    {
        var item = new WatchlistItem(Guid.NewGuid(), Guid.NewGuid(), null, AddedAt, ["Date Night ", "horror", "Horror"]);

        Assert.Equal(["date night", "horror"], item.Tags);
    }

    [Fact]
    public void Has_no_tags_by_default()
    {
        Assert.Empty(new WatchlistItem(Guid.NewGuid(), Guid.NewGuid(), null, AddedAt).Tags);
    }

    [Fact]
    public void UpdateTags_replaces_the_tags()
    {
        var item = new WatchlistItem(Guid.NewGuid(), Guid.NewGuid(), null, AddedAt, ["a"]);

        item.UpdateTags(["B", "c"]);

        Assert.Equal(["b", "c"], item.Tags);
    }
}
