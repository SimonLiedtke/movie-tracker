using System.ComponentModel.DataAnnotations;
using MovieTracker.Domain.Diary;
using DomainRating = MovieTracker.Domain.Diary.Rating;

namespace MovieTracker.Api.Contracts;

public sealed record DiaryEntryResponse(
    Guid Id,
    MovieRefResponse Movie,
    DateOnly WatchedOn,
    int? Rating,
    string? Review,
    bool IsRewatch,
    bool Liked,
    IReadOnlyList<string> Tags,
    DateTimeOffset CreatedAt);

public sealed class DiaryEntryRequest
{
    [Required]
    public Guid? MovieId { get; init; }

    [Required]
    public DateOnly? WatchedOn { get; init; }

    [Range(DomainRating.MinHalfStars, DomainRating.MaxHalfStars)]
    public int? Rating { get; init; }

    [StringLength(20000)]
    public string? Review { get; init; }

    public bool IsRewatch { get; init; }

    public bool Liked { get; init; }

    /// <summary>Free tags. Case and surrounding spaces do not matter; the response has them normalized.</summary>
    [ValidTags]
    public string[]? Tags { get; init; }

    // MovieId and WatchedOn are guaranteed by [Required] once model validation has passed.
    public DiaryEntryDetails ToDetails() => new(
        MovieId!.Value,
        WatchedOn!.Value,
        Rating is { } halfStars ? new DomainRating(halfStars) : (DomainRating?)null,
        Review,
        IsRewatch,
        Liked,
        Tags);
}
