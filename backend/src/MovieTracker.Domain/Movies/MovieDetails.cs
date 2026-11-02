namespace MovieTracker.Domain.Movies;

/// <summary>
/// The editable data of a movie. Used both for creating and for replacing a movie.
/// </summary>
public sealed record MovieDetails(
    string Title,
    string? OriginalTitle,
    DateOnly? ReleaseDate,
    int? RuntimeMinutes,
    string? Overview,
    string? PosterUrl,
    int? TmdbId);
