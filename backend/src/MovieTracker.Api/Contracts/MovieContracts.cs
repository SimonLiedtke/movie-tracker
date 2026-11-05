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

/// <summary>
/// One search hit: either one of the user's manually added movies (<see cref="MovieId"/> set, no <see cref="TmdbId"/>)
/// or a movie from TMDB (<see cref="TmdbId"/> set; <see cref="MovieId"/> only if it was imported before).
/// </summary>
public sealed record MovieSearchResult(
    Guid? MovieId,
    int? TmdbId,
    string Title,
    string? OriginalTitle,
    DateOnly? ReleaseDate,
    string? PosterUrl);

/// <param name="TmdbAvailable">False if TMDB is not configured or could not be reached; the items are then only manual movies.</param>
public sealed record MovieSearchResponse(IReadOnlyList<MovieSearchResult> Items, bool TmdbAvailable);

/// <summary>A manually added movie. Movies from TMDB are imported instead and cannot be edited.</summary>
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

    [Required]
    public List<int>? GenreIds { get; init; }

    public MovieDetails ToDetails() =>
        new(Title, OriginalTitle, ReleaseDate, RuntimeMinutes, Overview, PosterUrl);
}
