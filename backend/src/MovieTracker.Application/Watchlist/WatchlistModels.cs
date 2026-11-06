using MovieTracker.Application.Movies;

namespace MovieTracker.Application.Watchlist;

public sealed record WatchlistItemView(MovieRef Movie, DateTimeOffset AddedAt, string? Note);

public enum PutWatchlistItemStatus
{
    /// <summary>The movie was put on the watchlist now.</summary>
    Added,

    /// <summary>The movie was on the watchlist already; its note was replaced, the date added stays.</summary>
    Updated,

    /// <summary>The movie does not exist, or belongs to another user.</summary>
    NotFound,
}

public sealed record PutWatchlistItemResult(PutWatchlistItemStatus Status, WatchlistItemView? Item = null);
