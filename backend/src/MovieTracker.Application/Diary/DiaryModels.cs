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
    DateTimeOffset CreatedAt);

/// <summary>Only entries watched in the range (both ends included) and, if set, of this movie.</summary>
public sealed record DiaryFilter(DateOnly? From, DateOnly? To, Guid? MovieId);

public enum SaveDiaryEntryStatus
{
    Saved,

    /// <summary>The entry does not exist, or belongs to another user.</summary>
    NotFound,

    FutureDate,
    UnknownMovie,
}

public sealed record SaveDiaryEntryResult(SaveDiaryEntryStatus Status, DiaryEntryView? Entry = null);
