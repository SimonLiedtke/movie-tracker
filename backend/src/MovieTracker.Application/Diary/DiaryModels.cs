using MovieTracker.Application.Movies;
using MovieTracker.Domain.Diary;

namespace MovieTracker.Application.Diary;

public sealed record DiaryEntryView(
    Guid Id,
    MovieRef Movie,
    DateOnly WatchedOn,
    Rating? Rating,
    string? Review,
    bool IsRewatch,
    bool Liked,
    IReadOnlyList<string> Tags,
    DateTimeOffset CreatedAt);

/// <summary>Only entries watched in the range (both ends included) and, if set, of this movie, with this tag and rated in the range.</summary>
/// <param name="Tag">A tag in its normalized form (<see cref="Domain.Tags.TagList"/>); null: no restriction.</param>
/// <param name="RatingFrom">Entries rated at least this. Entries without a rating do not match.</param>
/// <param name="RatingTo">Entries rated at most this. Entries without a rating do not match.</param>
public sealed record DiaryFilter(
    DateOnly? From,
    DateOnly? To,
    Guid? MovieId,
    string? Tag = null,
    Rating? RatingFrom = null,
    Rating? RatingTo = null);

public enum SaveDiaryEntryStatus
{
    Saved,

    /// <summary>The entry does not exist, or belongs to another user.</summary>
    NotFound,

    FutureDate,
    UnknownMovie,
}

public sealed record SaveDiaryEntryResult(SaveDiaryEntryStatus Status, DiaryEntryView? Entry = null);
