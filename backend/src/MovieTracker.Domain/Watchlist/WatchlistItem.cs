using MovieTracker.Domain.Movies;

namespace MovieTracker.Domain.Watchlist;

public sealed class WatchlistItem
{
    private WatchlistItem()
    {
        // Required by EF Core.
    }

    public WatchlistItem(Guid movieId, string? note, DateTimeOffset addedAt)
    {
        MovieId = movieId;
        Note = note;
        AddedAt = addedAt;
    }

    public Guid MovieId { get; private set; }

    public Movie Movie { get; private set; } = null!;

    public DateTimeOffset AddedAt { get; private set; }

    public string? Note { get; private set; }

    public void UpdateNote(string? note) => Note = note;
}
