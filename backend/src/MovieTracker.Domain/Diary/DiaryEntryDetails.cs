namespace MovieTracker.Domain.Diary;

/// <summary>
/// The editable data of a diary entry. Used both for creating and for replacing an entry.
/// </summary>
public sealed record DiaryEntryDetails(
    Guid MovieId,
    DateOnly WatchedOn,
    Rating? Rating,
    string? Review,
    bool IsRewatch,
    bool Liked);
