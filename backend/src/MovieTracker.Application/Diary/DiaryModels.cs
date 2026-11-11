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

/// <summary>Only entries watched in the range (both ends included) and, if set, of this movie and with this tag.</summary>
/// <param name="Tag">A tag in its normalized form (<see cref="Domain.Tags.TagList"/>); null: no restriction.</param>
public sealed record DiaryFilter(DateOnly? From, DateOnly? To, Guid? MovieId, string? Tag = null);

public enum SaveDiaryEntryStatus
{
    Saved,

    /// <summary>The entry does not exist, or belongs to another user.</summary>
    NotFound,

    FutureDate,
    UnknownMovie,
}

public sealed record SaveDiaryEntryResult(SaveDiaryEntryStatus Status, DiaryEntryView? Entry = null);
