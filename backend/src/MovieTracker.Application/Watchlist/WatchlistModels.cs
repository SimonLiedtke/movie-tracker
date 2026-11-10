using MovieTracker.Application.Movies;
using MovieTracker.Domain.Movies;

namespace MovieTracker.Application.Watchlist;

/// <param name="Genres">The movie's genres, alphabetically.</param>
/// <param name="RuntimeMinutes">The movie's runtime; null if unknown.</param>
public sealed record WatchlistItemView(
    MovieRef Movie, DateTimeOffset AddedAt, string? Note, IReadOnlyList<Genre> Genres, int? RuntimeMinutes);

/// <summary>A page of the items matching a <see cref="WatchlistFilter"/>.</summary>
/// <param name="TotalCount">The number of items matching the filter.</param>
/// <param name="UnfilteredCount">The number of items on the watchlist, whatever the filter: how selective the filter is.</param>
public sealed record WatchlistPage(
    IReadOnlyList<WatchlistItemView> Items, int Page, int PageSize, int TotalCount, int UnfilteredCount);

/// <summary>How the genres of a <see cref="WatchlistFilter"/> are combined.</summary>
public enum GenreMatch
{
    /// <summary>The movie has at least one of the genres.</summary>
    Any,

    /// <summary>The movie has every one of the genres (and possibly more).</summary>
    All,
}

/// <summary>
/// Only items that match every part of the filter; a part that is not set does not restrict.
/// </summary>
/// <param name="GenreIds">
/// Movies with these genres, see <paramref name="GenreMatch"/>. Genres that no movie has are not an error; they match
/// nothing with <see cref="GenreMatch.All"/> and are ignored with <see cref="GenreMatch.Any"/>.
/// </param>
/// <param name="ReleasedFrom">Movies released on or after this day. Movies without a release date do not match.</param>
/// <param name="ReleasedTo">Movies released on or before this day. Movies without a release date do not match.</param>
/// <param name="AddedFrom">Items put on the watchlist on or after this day (UTC).</param>
/// <param name="AddedTo">Items put on the watchlist on or before this day (UTC).</param>
/// <param name="RuntimeFrom">Movies that are at least this many minutes long. Movies without a runtime do not match.</param>
/// <param name="RuntimeTo">Movies that are at most this many minutes long. Movies without a runtime do not match.</param>
public sealed record WatchlistFilter(
    IReadOnlyList<int> GenreIds,
    GenreMatch GenreMatch,
    DateOnly? ReleasedFrom = null,
    DateOnly? ReleasedTo = null,
    DateOnly? AddedFrom = null,
    DateOnly? AddedTo = null,
    int? RuntimeFrom = null,
    int? RuntimeTo = null)
{
    public static WatchlistFilter None { get; } = new([], GenreMatch.Any);

    /// <summary>True if every item matches.</summary>
    public bool IsEmpty =>
        GenreIds.Count == 0
        && ReleasedFrom is null && ReleasedTo is null
        && AddedFrom is null && AddedTo is null
        && RuntimeFrom is null && RuntimeTo is null;
}

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
