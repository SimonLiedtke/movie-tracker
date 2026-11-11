using MovieTracker.Domain.Movies;
using MovieTracker.Domain.Tags;

namespace MovieTracker.Domain.Watchlist;

public sealed class WatchlistItem
{
    private WatchlistItem()
    {
        // Required by EF Core.
    }

    public WatchlistItem(Guid userId, Guid movieId, string? note, DateTimeOffset addedAt, IReadOnlyList<string>? tags = null)
    {
        UserId = userId;
        MovieId = movieId;
        Note = note;
        AddedAt = addedAt;
        Tags = TagList.Normalize(tags);
    }

    /// <summary>The owner. Every user has their own watchlist.</summary>
    public Guid UserId { get; private set; }

    public Guid MovieId { get; private set; }

    public Movie Movie { get; private set; } = null!;

    public DateTimeOffset AddedAt { get; private set; }

    public string? Note { get; private set; }

    /// <summary>In their normalized form (<see cref="TagList.Normalize"/>).</summary>
    public string[] Tags { get; private set; } = [];

    public void UpdateNote(string? note) => Note = note;

    public void UpdateTags(IEnumerable<string>? tags) => Tags = TagList.Normalize(tags);
}
