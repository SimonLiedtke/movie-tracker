using MovieTracker.Domain.Movies;

namespace MovieTracker.Infrastructure.Persistence.Seed;

/// <summary>
/// Movie genres with the IDs used by TMDB, so a later TMDB import can map genres directly.
/// </summary>
internal static class GenreSeed
{
    public static IReadOnlyList<Genre> All { get; } =
    [
        new(28, "Action"),
        new(12, "Adventure"),
        new(16, "Animation"),
        new(35, "Comedy"),
        new(80, "Crime"),
        new(99, "Documentary"),
        new(18, "Drama"),
        new(10751, "Family"),
        new(14, "Fantasy"),
        new(36, "History"),
        new(27, "Horror"),
        new(10402, "Music"),
        new(9648, "Mystery"),
        new(10749, "Romance"),
        new(878, "Science Fiction"),
        new(10770, "TV Movie"),
        new(53, "Thriller"),
        new(10752, "War"),
        new(37, "Western"),
    ];
}
