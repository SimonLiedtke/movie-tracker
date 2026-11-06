using MovieTracker.Application.Diary;
using MovieTracker.Application.Movies;
using MovieTracker.Domain.Diary;

namespace MovieTracker.Infrastructure.Persistence.Queries;

internal static class DiaryEntryQueries
{
    public static IOrderedQueryable<DiaryEntry> OrderByNewest(this IQueryable<DiaryEntry> entries) =>
        entries
            .OrderByDescending(entry => entry.WatchedOn)
            .ThenByDescending(entry => entry.CreatedAt)
            .ThenByDescending(entry => entry.Id);

    public static IQueryable<DiaryEntryRow> SelectRows(this IQueryable<DiaryEntry> entries) =>
        entries.Select(entry => new DiaryEntryRow(
            entry.Id,
            new MovieRef(entry.Movie.Id, entry.Movie.Title, entry.Movie.ReleaseDate, entry.Movie.PosterUrl),
            entry.WatchedOn,
            entry.Rating,
            entry.Review,
            entry.IsRewatch,
            entry.Liked,
            entry.CreatedAt));
}

/// <summary>Rating is a value-converted type that EF Core cannot dereference in SQL, so the queries project into rows first.</summary>
internal sealed record DiaryEntryRow(
    Guid Id,
    MovieRef Movie,
    DateOnly WatchedOn,
    Rating? Rating,
    string? Review,
    bool IsRewatch,
    bool Liked,
    DateTimeOffset CreatedAt)
{
    public DiaryEntryView ToModel() => new(Id, Movie, WatchedOn, Rating, Review, IsRewatch, Liked, CreatedAt);
}
