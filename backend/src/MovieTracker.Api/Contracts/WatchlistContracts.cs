using System.ComponentModel.DataAnnotations;

namespace MovieTracker.Api.Contracts;

public sealed record WatchlistItemResponse(
    MovieRefResponse Movie,
    DateTimeOffset AddedAt,
    string? Note,
    IReadOnlyList<GenreResponse> Genres,
    int? RuntimeMinutes);

/// <summary>A page of the watchlist. <paramref name="UnfilteredCount"/>: all items, whatever the filter.</summary>
public sealed record WatchlistPageResponse(
    IReadOnlyList<WatchlistItemResponse> Items, int Page, int PageSize, int TotalCount, int UnfilteredCount);

public sealed class WatchlistItemRequest
{
    [StringLength(500)]
    public string? Note { get; init; }
}
