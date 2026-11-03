using Microsoft.EntityFrameworkCore;
using MovieTracker.Api.Contracts;
using MovieTracker.Domain.Diary;
using MovieTracker.Domain.Movies;
using MovieTracker.Infrastructure.Persistence;

namespace MovieTracker.Api.Queries;

/// <summary>
/// Projections from movies to response rows. Rating is a value-converted type that EF Core
/// cannot dereference in SQL, so the queries project into rows first and map to responses in memory.
/// </summary>
internal static class MovieQueries
{
    private const string LikeEscapeCharacter = @"\";

    public static IQueryable<Movie> Search(this IQueryable<Movie> movies, string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return movies;
        }

        var pattern = $"%{EscapeLikePattern(search.Trim())}%";
        // The escape character must be passed explicitly: without it, Npgsql generates ESCAPE '' and the
        // backslashes added by EscapeLikePattern would be matched literally.
        return movies.Where(movie =>
            EF.Functions.ILike(movie.Title, pattern, LikeEscapeCharacter) ||
            (movie.OriginalTitle != null && EF.Functions.ILike(movie.OriginalTitle, pattern, LikeEscapeCharacter)));
    }

    public static IQueryable<MovieSummaryRow> SelectSummaryRows(
        this IQueryable<Movie> movies, MovieTrackerDbContext db) =>
        movies.Select(movie => new MovieSummaryRow(
            movie.Id,
            movie.Title,
            movie.ReleaseDate,
            movie.PosterUrl,
            movie.Genres
                .OrderBy(genre => genre.Name)
                .Select(genre => new GenreResponse(genre.Id, genre.Name))
                .ToList(),
            db.DiaryEntries
                .Where(entry => entry.MovieId == movie.Id && entry.Rating != null)
                .OrderByDescending(entry => entry.WatchedOn)
                .ThenByDescending(entry => entry.CreatedAt)
                .Select(entry => entry.Rating)
                .FirstOrDefault(),
            db.DiaryEntries.Count(entry => entry.MovieId == movie.Id),
            db.WatchlistItems.Any(item => item.MovieId == movie.Id)));

    public static IQueryable<MovieDetailRow> SelectDetailRows(
        this IQueryable<Movie> movies, MovieTrackerDbContext db) =>
        movies.Select(movie => new MovieDetailRow(
            movie.Id,
            movie.Title,
            movie.OriginalTitle,
            movie.ReleaseDate,
            movie.RuntimeMinutes,
            movie.Overview,
            movie.PosterUrl,
            movie.TmdbId,
            movie.Genres
                .OrderBy(genre => genre.Name)
                .Select(genre => new GenreResponse(genre.Id, genre.Name))
                .ToList(),
            db.DiaryEntries
                .Where(entry => entry.MovieId == movie.Id && entry.Rating != null)
                .OrderByDescending(entry => entry.WatchedOn)
                .ThenByDescending(entry => entry.CreatedAt)
                .Select(entry => entry.Rating)
                .FirstOrDefault(),
            db.DiaryEntries.Count(entry => entry.MovieId == movie.Id),
            db.WatchlistItems.Any(item => item.MovieId == movie.Id),
            db.DiaryEntries
                .Where(entry => entry.MovieId == movie.Id)
                .Max(entry => (DateOnly?)entry.WatchedOn),
            movie.CreatedAt));

    private static string EscapeLikePattern(string value) => value
        .Replace(@"\", @"\\")
        .Replace("%", @"\%")
        .Replace("_", @"\_");
}

internal sealed record MovieSummaryRow(
    Guid Id,
    string Title,
    DateOnly? ReleaseDate,
    string? PosterUrl,
    List<GenreResponse> Genres,
    Rating? LatestRating,
    int WatchCount,
    bool OnWatchlist)
{
    public MovieSummaryResponse ToResponse() => new(
        Id, Title, ReleaseDate, PosterUrl, Genres, LatestRating?.HalfStars, WatchCount, OnWatchlist);
}

internal sealed record MovieDetailRow(
    Guid Id,
    string Title,
    string? OriginalTitle,
    DateOnly? ReleaseDate,
    int? RuntimeMinutes,
    string? Overview,
    string? PosterUrl,
    int? TmdbId,
    List<GenreResponse> Genres,
    Rating? LatestRating,
    int WatchCount,
    bool OnWatchlist,
    DateOnly? LastWatchedOn,
    DateTimeOffset CreatedAt)
{
    public MovieResponse ToResponse() => new(
        Id, Title, OriginalTitle, ReleaseDate, RuntimeMinutes, Overview, PosterUrl, TmdbId, Genres,
        LatestRating?.HalfStars, WatchCount, OnWatchlist, LastWatchedOn, CreatedAt);
}
