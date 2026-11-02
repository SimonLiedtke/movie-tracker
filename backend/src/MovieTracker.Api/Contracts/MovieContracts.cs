using System.ComponentModel.DataAnnotations;
using MovieTracker.Domain.Movies;

namespace MovieTracker.Api.Contracts;

public sealed record MovieRefResponse(Guid Id, string Title, DateOnly? ReleaseDate, string? PosterUrl);

public sealed record MovieSummaryResponse(
    Guid Id,
    string Title,
    DateOnly? ReleaseDate,
    string? PosterUrl,
    IReadOnlyList<GenreResponse> Genres,
    int? LatestRating,
    int WatchCount,
    bool OnWatchlist);

public sealed record MovieResponse(
    Guid Id,
    string Title,
    string? OriginalTitle,
    DateOnly? ReleaseDate,
    int? RuntimeMinutes,
    string? Overview,
    string? PosterUrl,
    int? TmdbId,
    IReadOnlyList<GenreResponse> Genres,
    int? LatestRating,
    int WatchCount,
    bool OnWatchlist,
    DateOnly? LastWatchedOn,
    DateTimeOffset CreatedAt);

public sealed class MovieRequest
{
    [Required]
    [StringLength(300)]
    public string Title { get; init; } = string.Empty;

    [StringLength(300)]
    public string? OriginalTitle { get; init; }

    public DateOnly? ReleaseDate { get; init; }

    [Range(1, 1500)]
    public int? RuntimeMinutes { get; init; }

    [StringLength(5000)]
    public string? Overview { get; init; }

    [Url]
    [StringLength(2000)]
    public string? PosterUrl { get; init; }

    [Range(1, int.MaxValue)]
    public int? TmdbId { get; init; }

    [Required]
    public List<int>? GenreIds { get; init; }

    public MovieDetails ToDetails() =>
        new(Title, OriginalTitle, ReleaseDate, RuntimeMinutes, Overview, PosterUrl, TmdbId);
}
