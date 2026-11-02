using System.ComponentModel.DataAnnotations;

namespace MovieTracker.Api.Contracts;

public sealed record WatchlistItemResponse(MovieRefResponse Movie, DateTimeOffset AddedAt, string? Note);

public sealed class WatchlistItemRequest
{
    [StringLength(500)]
    public string? Note { get; init; }
}
