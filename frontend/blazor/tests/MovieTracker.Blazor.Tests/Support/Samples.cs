using MovieTracker.Blazor.Core.Api;

namespace MovieTracker.Blazor.Tests.Support;

/// <summary>Test data.</summary>
public static class Samples
{
    public static readonly User Jane = new(Guid.Parse("00000000-0000-0000-0000-0000000000aa"), "jane@example.com", "Jane Doe", null);

    public static readonly Genre Action = new(1, "Action");
    public static readonly Genre Comedy = new(2, "Comedy");
    public static readonly Genre Drama = new(3, "Drama");
    public static readonly Genre[] Genres = [Action, Comedy, Drama];

    public static Guid Id(int number) => new(number, 0, 0, [0, 0, 0, 0, 0, 0, 0, 0]);

    public static MovieRef Ref(int number, string? title = null, string? posterUrl = null) =>
        new(Id(number), title ?? $"Movie {number}", new DateOnly(1980 + number, 5, 17), posterUrl);

    public static MovieSummary Summary(int number, int? rating = null, int watchCount = 0, bool onWatchlist = false) =>
        new(Id(number), $"Movie {number}", new DateOnly(1980 + number, 5, 17), null, [Action], rating, watchCount, onWatchlist);

    public static Movie Movie(
        int number,
        int? tmdbId = 100,
        bool onWatchlist = false,
        int watchCount = 0,
        string? posterUrl = null,
        string? originalTitle = null) =>
        new(
            Id(number), $"Movie {number}", originalTitle, new DateOnly(1980 + number, 5, 17), 100 + number,
            $"Overview {number}", posterUrl, tmdbId, [Action, Drama], null, watchCount, onWatchlist, null,
            new DateTimeOffset(2026, 10, 1, 10, 0, 0, TimeSpan.Zero));

    public static DiaryEntry Entry(
        int number, int movie = 1, DateOnly? watchedOn = null, int? rating = null, string? review = null,
        bool isRewatch = false, bool liked = false, params string[] tags) =>
        new(Id(1000 + number), Ref(movie), watchedOn ?? new DateOnly(2026, 11, 7), rating, review, isRewatch, liked, tags,
            new DateTimeOffset(2026, 11, 7, 20, 0, 0, TimeSpan.Zero));

    public static PagedResponse<T> Page<T>(IReadOnlyList<T> items, int page = 1, int pageSize = 25, int? total = null) =>
        new(items, page, pageSize, total ?? items.Count);

    public static WatchlistItem Item(int number, string? note = null, int? runtime = 100, params string[] tags) =>
        new(Ref(number), new DateTimeOffset(2026, 11, 3, 9, 0, 0, TimeSpan.Zero), note, tags, [Comedy], runtime);

    public static WatchlistPage WatchlistPage(IReadOnlyList<WatchlistItem> items, int page = 1, int total = -1, int unfiltered = -1) =>
        new(items, page, 20, total < 0 ? items.Count : total, unfiltered < 0 ? items.Count : unfiltered);
}
