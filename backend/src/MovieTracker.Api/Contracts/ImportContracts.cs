using System.Text.Json.Serialization;

namespace MovieTracker.Api.Contracts;

[JsonConverter(typeof(JsonStringEnumConverter<ImportStatus>))]
public enum ImportStatus
{
    /// <summary>Waiting or in progress.</summary>
    [JsonStringEnumMemberName("running")]
    Running,

    [JsonStringEnumMemberName("completed")]
    Completed,

    [JsonStringEnumMemberName("failed")]
    Failed,
}

/// <param name="FilmsTotal">The number of different films in the diary and the watchlist. 0 until the import has started.</param>
/// <param name="FilmsProcessed">How many of them have been looked up so far.</param>
/// <param name="Result">Set once the import has completed.</param>
/// <param name="Error">Set if the import has failed.</param>
public sealed record ImportJobResponse(
    Guid Id,
    ImportStatus Status,
    int FilmsTotal,
    int FilmsProcessed,
    ImportResultResponse? Result,
    string? Error);

/// <param name="DiaryEntriesSkipped">Viewings that were in the diary already (same movie, same day), e.g. from an earlier import.</param>
/// <param name="WatchlistItemsSkipped">Films that were on the watchlist already.</param>
/// <param name="ManualMovies">Films that TMDB does not know (or the server has no TMDB access): added as manual movies.</param>
/// <param name="FailedFilms">Films that could not be looked up because TMDB failed. They were not imported; importing again retries them.</param>
/// <param name="Warnings">Rows of the export that were skipped, and why.</param>
public sealed record ImportResultResponse(
    int DiaryEntriesImported,
    int DiaryEntriesSkipped,
    int WatchlistItemsImported,
    int WatchlistItemsSkipped,
    IReadOnlyList<ImportFilmResponse> ManualMovies,
    IReadOnlyList<ImportFilmResponse> FailedFilms,
    IReadOnlyList<string> Warnings);

public sealed record ImportFilmResponse(string Name, int? Year);
