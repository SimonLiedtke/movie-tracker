namespace MovieTracker.Domain.Movies;

/// <summary>
/// The descriptive data of a movie, typed in by a user or loaded from TMDB.
/// </summary>
public sealed record MovieDetails(
    string Title,
    string? OriginalTitle,
    DateOnly? ReleaseDate,
    int? RuntimeMinutes,
    string? Overview,
    string? PosterUrl);
