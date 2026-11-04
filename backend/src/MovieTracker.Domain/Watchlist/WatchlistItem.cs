using MovieTracker.Domain.Movies;

namespace MovieTracker.Domain.Watchlist;

public sealed class WatchlistItem
{
    private WatchlistItem()
    {
        // Required by EF Core.
    }

    public WatchlistItem(Guid userId, Guid movieId, string? note, DateTimeOffset addedAt)
    {
        UserId = userId;
        MovieId = movieId;
        Note = note;
        AddedAt = addedAt;
    }

    /// <summary>The owner. Every user has their own watchlist.</summary>
    public Guid UserId { get; private set; }

    public Guid MovieId { get; private set; }

    public Movie Movie { get; private set; } = null!;

    public DateTimeOffset AddedAt { get; private set; }

    public string? Note { get; private set; }

    public void UpdateNote(string? note) => Note = note;
}
