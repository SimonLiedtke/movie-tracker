using Microsoft.EntityFrameworkCore;
using MovieTracker.Application.Movies;
using MovieTracker.Application.Watchlist;
using MovieTracker.Domain.Diary;
using MovieTracker.Domain.Movies;
using MovieTracker.Domain.Watchlist;

namespace MovieTracker.Infrastructure.Persistence.Queries;

/// <summary>
/// Filters and projections for movies. TMDB movies are shared, manually added movies are private to their
/// owner, and the statistics (rating, watch count, watchlist) are those of the given user only. Rating is a value-converted type that EF Core
/// cannot dereference in SQL, so the queries project into rows first and map to the read models in memory.
/// </summary>
internal static class MovieQueries
{
    private const string LikeEscapeCharacter = @"\";

    /// <summary>Movies from TMDB and the user's own manually added movies. Other users' movies do not exist for them.</summary>
    public static IQueryable<Movie> VisibleTo(this IQueryable<Movie> movies, Guid userId) =>
        movies.Where(movie => movie.OwnerId == null || movie.OwnerId == userId);

    /// <summary>"My movies": added manually by the user, or in their diary or on their watchlist.</summary>
    public static IQueryable<Movie> InLibraryOf(this IQueryable<Movie> movies, MovieTrackerDbContext db, Guid userId) =>
        movies.Where(movie =>
            movie.OwnerId == userId ||
            db.DiaryEntries.Any(entry => entry.MovieId == movie.Id && entry.UserId == userId) ||
            db.WatchlistItems.Any(item => item.MovieId == movie.Id && item.UserId == userId));

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

    /// <summary>Watchlist items that match the filter.</summary>
    public static IQueryable<WatchlistItem> Matching(this IQueryable<WatchlistItem> items, WatchlistFilter filter)
    {
        var genreIds = filter.GenreIds.Distinct().ToList();
        if (genreIds.Count > 0)
        {
            items = filter.GenreMatch == GenreMatch.All
                // Genres are assigned to a movie at most once, so counting the matches counts the distinct genres.
                ? items.Where(item => item.Movie.Genres.Count(genre => genreIds.Contains(genre.Id)) == genreIds.Count)
                : items.Where(item => item.Movie.Genres.Any(genre => genreIds.Contains(genre.Id)));
        }

        // Comparing a missing release date is never true: such movies are left out if a bound is set.
        if (filter.ReleasedFrom is { } from)
        {
            items = items.Where(item => item.Movie.ReleaseDate >= from);
        }

        if (filter.ReleasedTo is { } to)
        {
            items = items.Where(item => item.Movie.ReleaseDate <= to);
        }

        // Like the release date, a missing runtime never compares true.
        if (filter.RuntimeFrom is { } runtimeFrom)
        {
            items = items.Where(item => item.Movie.RuntimeMinutes >= runtimeFrom);
        }

        if (filter.RuntimeTo is { } runtimeTo)
        {
            items = items.Where(item => item.Movie.RuntimeMinutes <= runtimeTo);
        }

        // Days are UTC days, like the date the frontends show for "added".
        if (filter.AddedFrom is { } addedFrom)
        {
            var start = StartOfDay(addedFrom);
            items = items.Where(item => item.AddedAt >= start);
        }

        // Exclusive upper bound: the start of the day after. For the last possible day there is none.
        if (filter.AddedTo is { } addedTo && addedTo < DateOnly.MaxValue)
        {
            var end = StartOfDay(addedTo.AddDays(1));
            items = items.Where(item => item.AddedAt < end);
        }

        return items;
    }

    private static DateTimeOffset StartOfDay(DateOnly day) =>
        new(day.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

    public static IQueryable<MovieSummaryRow> SelectSummaryRows(
        this IQueryable<Movie> movies, MovieTrackerDbContext db, Guid userId) =>
        movies.Select(movie => new MovieSummaryRow(
            movie.Id,
            movie.Title,
            movie.ReleaseDate,
            movie.PosterUrl,
            movie.Genres
                .OrderBy(genre => genre.Name)
                .Select(genre => new GenreRow(genre.Id, genre.Name))
                .ToList(),
            db.DiaryEntries
                .Where(entry => entry.MovieId == movie.Id && entry.UserId == userId && entry.Rating != null)
                .OrderByDescending(entry => entry.WatchedOn)
                .ThenByDescending(entry => entry.CreatedAt)
                .Select(entry => entry.Rating)
                .FirstOrDefault(),
            db.DiaryEntries.Count(entry => entry.MovieId == movie.Id && entry.UserId == userId),
            db.WatchlistItems.Any(item => item.MovieId == movie.Id && item.UserId == userId)));

    public static IQueryable<MovieDetailRow> SelectDetailRows(
        this IQueryable<Movie> movies, MovieTrackerDbContext db, Guid userId) =>
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
                .Select(genre => new GenreRow(genre.Id, genre.Name))
                .ToList(),
            db.DiaryEntries
                .Where(entry => entry.MovieId == movie.Id && entry.UserId == userId && entry.Rating != null)
                .OrderByDescending(entry => entry.WatchedOn)
                .ThenByDescending(entry => entry.CreatedAt)
                .Select(entry => entry.Rating)
                .FirstOrDefault(),
            db.DiaryEntries.Count(entry => entry.MovieId == movie.Id && entry.UserId == userId),
            db.WatchlistItems.Any(item => item.MovieId == movie.Id && item.UserId == userId),
            db.DiaryEntries
                .Where(entry => entry.MovieId == movie.Id && entry.UserId == userId)
                .Max(entry => (DateOnly?)entry.WatchedOn),
            movie.CreatedAt));

    private static string EscapeLikePattern(string value) => value
        .Replace(@"\", @"\\")
        .Replace("%", @"\%")
        .Replace("_", @"\_");
}

internal sealed record GenreRow(int Id, string Name)
{
    public Genre ToModel() => new(Id, Name);
}

internal sealed record MovieSummaryRow(
    Guid Id,
    string Title,
    DateOnly? ReleaseDate,
    string? PosterUrl,
    List<GenreRow> Genres,
    Rating? LatestRating,
    int WatchCount,
    bool OnWatchlist)
{
    public MovieSummary ToModel() => new(
        Id, Title, ReleaseDate, PosterUrl, Genres.Select(genre => genre.ToModel()).ToList(), LatestRating, WatchCount, OnWatchlist);
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
    List<GenreRow> Genres,
    Rating? LatestRating,
    int WatchCount,
    bool OnWatchlist,
    DateOnly? LastWatchedOn,
    DateTimeOffset CreatedAt)
{
    public MovieDetail ToModel() => new(
        Id, Title, OriginalTitle, ReleaseDate, RuntimeMinutes, Overview, PosterUrl, TmdbId,
        Genres.Select(genre => genre.ToModel()).ToList(), LatestRating, WatchCount, OnWatchlist, LastWatchedOn, CreatedAt);
}
