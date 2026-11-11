namespace MovieTracker.Domain.Diary;

/// <summary>
/// The editable data of a diary entry. Used both for creating and for replacing an entry.
/// </summary>
/// <param name="Tags">Free tags; the entry stores them normalized, see <see cref="MovieTracker.Domain.Tags.TagList"/>. Null: none.</param>
public sealed record DiaryEntryDetails(
    Guid MovieId,
    DateOnly WatchedOn,
    Rating? Rating,
    string? Review,
    bool IsRewatch,
    bool Liked,
    IReadOnlyList<string>? Tags = null);
