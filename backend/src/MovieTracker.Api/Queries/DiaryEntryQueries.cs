using MovieTracker.Api.Contracts;
using MovieTracker.Domain.Diary;

namespace MovieTracker.Api.Queries;

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
            new MovieRefResponse(entry.Movie.Id, entry.Movie.Title, entry.Movie.ReleaseDate, entry.Movie.PosterUrl),
            entry.WatchedOn,
            entry.Rating,
            entry.Review,
            entry.IsRewatch,
            entry.Liked,
            entry.CreatedAt));
}

internal sealed record DiaryEntryRow(
    Guid Id,
    MovieRefResponse Movie,
    DateOnly WatchedOn,
    Rating? Rating,
    string? Review,
    bool IsRewatch,
    bool Liked,
    DateTimeOffset CreatedAt)
{
    public DiaryEntryResponse ToResponse() => new(
        Id, Movie, WatchedOn, Rating?.HalfStars, Review, IsRewatch, Liked, CreatedAt);
}
