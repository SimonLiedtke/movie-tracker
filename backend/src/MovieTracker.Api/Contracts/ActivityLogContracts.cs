using System.Text.Json.Serialization;

namespace MovieTracker.Api.Contracts;

/// <summary>What the user did.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ActivityLogType>))]
public enum ActivityLogType
{
    [JsonStringEnumMemberName("diaryEntryAdded")]
    DiaryEntryAdded,

    [JsonStringEnumMemberName("diaryEntryUpdated")]
    DiaryEntryUpdated,

    [JsonStringEnumMemberName("diaryEntryDeleted")]
    DiaryEntryDeleted,

    [JsonStringEnumMemberName("watchlistItemAdded")]
    WatchlistItemAdded,

    [JsonStringEnumMemberName("watchlistNoteChanged")]
    WatchlistNoteChanged,

    [JsonStringEnumMemberName("watchlistTagsChanged")]
    WatchlistTagsChanged,

    [JsonStringEnumMemberName("watchlistItemRemoved")]
    WatchlistItemRemoved,

    [JsonStringEnumMemberName("movieImported")]
    MovieImported,

    [JsonStringEnumMemberName("movieAdded")]
    MovieAdded,

    [JsonStringEnumMemberName("movieUpdated")]
    MovieUpdated,

    [JsonStringEnumMemberName("movieDeleted")]
    MovieDeleted,

    [JsonStringEnumMemberName("letterboxdImported")]
    LetterboxdImported,
}

/// <param name="OccurredAt">When the user did it.</param>
/// <param name="MovieId">The movie it was about; null if it was not about one movie, or the movie was deleted since.</param>
/// <param name="MovieTitle">The movie's title at the time; null if the action was not about one movie.</param>
/// <param name="Detail">What exactly happened, in a few words; null if the type says it all.</param>
public sealed record ActivityLogEntryResponse(
    Guid Id,
    ActivityLogType Type,
    DateTimeOffset OccurredAt,
    Guid? MovieId,
    string? MovieTitle,
    string? Detail);
