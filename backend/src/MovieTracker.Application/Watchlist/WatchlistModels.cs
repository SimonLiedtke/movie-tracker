using MovieTracker.Application.Movies;
using MovieTracker.Domain.Movies;

namespace MovieTracker.Application.Watchlist;

/// <param name="Genres">The movie's genres, alphabetically.</param>
public sealed record WatchlistItemView(MovieRef Movie, DateTimeOffset AddedAt, string? Note, IReadOnlyList<Genre> Genres);

/// <summary>How the genres of a <see cref="WatchlistFilter"/> are combined.</summary>
public enum GenreMatch
{
    /// <summary>The movie has at least one of the genres.</summary>
    Any,

    /// <summary>The movie has every one of the genres (and possibly more).</summary>
    All,
}

/// <summary>
/// Only items whose movie matches the genres. No genres: no restriction. Genres that no movie has are not an error;
/// they match nothing with <see cref="GenreMatch.All"/> and are ignored with <see cref="GenreMatch.Any"/>.
/// </summary>
public sealed record WatchlistFilter(IReadOnlyList<int> GenreIds, GenreMatch GenreMatch)
{
    public static WatchlistFilter None { get; } = new([], GenreMatch.Any);
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
