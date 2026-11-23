using System.Text.Json.Serialization;

namespace MovieTracker.Blazor.Core.Api;

// C# mirror of the backend contracts (backend/src/MovieTracker.Api/Contracts).
// Dates without time are DateOnly ("2026-10-31").

public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

public sealed record Genre(int Id, string Name);

public sealed record MovieRef(Guid Id, string Title, DateOnly? ReleaseDate, string? PosterUrl);

/// <param name="LatestRating">Half stars: 1 = 0.5 stars, 10 = 5 stars.</param>
public sealed record MovieSummary(
    Guid Id,
    string Title,
    DateOnly? ReleaseDate,
    string? PosterUrl,
    IReadOnlyList<Genre> Genres,
    int? LatestRating,
    int WatchCount,
    bool OnWatchlist);

/// <param name="TmdbId">Set for movies from TMDB (shared, read-only). Null for movies the user added manually (private, editable).</param>
/// <param name="LatestRating">Half stars: 1 = 0.5 stars, 10 = 5 stars.</param>
public sealed record Movie(
    Guid Id,
    string Title,
    string? OriginalTitle,
    DateOnly? ReleaseDate,
    int? RuntimeMinutes,
    string? Overview,
    string? PosterUrl,
    int? TmdbId,
    IReadOnlyList<Genre> Genres,
    int? LatestRating,
    int WatchCount,
    bool OnWatchlist,
    DateOnly? LastWatchedOn,
    DateTimeOffset CreatedAt)
{
    public MovieRef ToRef() => new(Id, Title, ReleaseDate, PosterUrl);
}

public sealed record MovieRequest(
    string Title,
    string? OriginalTitle,
    DateOnly? ReleaseDate,
    int? RuntimeMinutes,
    string? Overview,
    string? PosterUrl,
    IReadOnlyList<int> GenreIds);

/// <summary>
/// A search hit: one of the user's manually added movies (<see cref="MovieId"/> set, <see cref="TmdbId"/> null)
/// or a TMDB movie (<see cref="TmdbId"/> set; <see cref="MovieId"/> only if it was imported before).
/// </summary>
public sealed record MovieSearchResult(
    Guid? MovieId,
    int? TmdbId,
    string Title,
    string? OriginalTitle,
    DateOnly? ReleaseDate,
    string? PosterUrl);

/// <param name="TmdbAvailable">False if the server has no TMDB access or TMDB is down: the items are then only manual movies.</param>
public sealed record MovieSearchResponse(IReadOnlyList<MovieSearchResult> Items, bool TmdbAvailable);

/// <summary>The sort orders of the movie list ("-" is descending).</summary>
public static class MovieSort
{
    public const string Title = "title";
    public const string TitleDescending = "-title";
    public const string ReleaseDate = "releaseDate";
    public const string ReleaseDateDescending = "-releaseDate";
    public const string CreatedAt = "createdAt";
    public const string CreatedAtDescending = "-createdAt";
}

public sealed record MovieQuery(
    string? Search = null,
    int? GenreId = null,
    int? Year = null,
    string? Sort = null,
    int? Page = null,
    int? PageSize = null);

/// <param name="Rating">Half stars: 1 = 0.5 stars, 10 = 5 stars.</param>
/// <param name="Tags">Normalized by the server: lower case, alphabetical.</param>
public sealed record DiaryEntry(
    Guid Id,
    MovieRef Movie,
    DateOnly WatchedOn,
    int? Rating,
    string? Review,
    bool IsRewatch,
    bool Liked,
    IReadOnlyList<string> Tags,
    DateTimeOffset CreatedAt);

public sealed record DiaryEntryRequest(
    Guid MovieId,
    DateOnly WatchedOn,
    int? Rating,
    string? Review,
    bool IsRewatch,
    bool Liked,
    IReadOnlyList<string> Tags);

/// <param name="Tag">Only entries with this tag (case does not matter).</param>
/// <param name="RatingFrom">Only entries rated at least this, in half stars (1-10). Unrated entries do not match.</param>
/// <param name="RatingTo">Only entries rated at most this, in half stars (1-10). Unrated entries do not match.</param>
public sealed record DiaryQuery(
    string? From = null,
    string? To = null,
    Guid? MovieId = null,
    string? Tag = null,
    int? RatingFrom = null,
    int? RatingTo = null,
    int? Page = null,
    int? PageSize = null);

/// <param name="Tags">Normalized by the server: lower case, alphabetical.</param>
/// <param name="Genres">The movie's genres, alphabetically.</param>
/// <param name="RuntimeMinutes">The movie's runtime in minutes; null if unknown.</param>
public sealed record WatchlistItem(
    MovieRef Movie,
    DateTimeOffset AddedAt,
    string? Note,
    IReadOnlyList<string> Tags,
    IReadOnlyList<Genre> Genres,
    int? RuntimeMinutes);

/// <summary>A page of the watchlist.</summary>
/// <param name="UnfilteredCount">The number of items on the watchlist, whatever the filter; <c>TotalCount</c> counts the matching ones.</param>
public sealed record WatchlistPage(
    IReadOnlyList<WatchlistItem> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int UnfilteredCount);

public sealed record WatchlistItemRequest(string? Note, IReadOnlyList<string> Tags);

[JsonConverter(typeof(JsonStringEnumConverter<ActivityType>))]
public enum ActivityType
{
    [JsonStringEnumMemberName("diaryEntryAdded")] DiaryEntryAdded,
    [JsonStringEnumMemberName("diaryEntryUpdated")] DiaryEntryUpdated,
    [JsonStringEnumMemberName("diaryEntryDeleted")] DiaryEntryDeleted,
    [JsonStringEnumMemberName("watchlistItemAdded")] WatchlistItemAdded,
    [JsonStringEnumMemberName("watchlistNoteChanged")] WatchlistNoteChanged,
    [JsonStringEnumMemberName("watchlistTagsChanged")] WatchlistTagsChanged,
    [JsonStringEnumMemberName("watchlistItemRemoved")] WatchlistItemRemoved,
    [JsonStringEnumMemberName("movieImported")] MovieImported,
    [JsonStringEnumMemberName("movieAdded")] MovieAdded,
    [JsonStringEnumMemberName("movieUpdated")] MovieUpdated,
    [JsonStringEnumMemberName("movieDeleted")] MovieDeleted,
    [JsonStringEnumMemberName("letterboxdImported")] LetterboxdImported,
}

/// <summary>Something the user did that modified the database.</summary>
/// <param name="OccurredAt">When it happened.</param>
/// <param name="MovieId">The movie it was about. Null if it was not about one movie, or the movie was deleted since.</param>
/// <param name="MovieTitle">The movie's title when it happened. Null if it was not about one movie.</param>
/// <param name="Detail">What exactly happened, in a few words (e.g. the rating or the note).</param>
public sealed record ActivityLogEntry(
    Guid Id,
    ActivityType Type,
    DateTimeOffset OccurredAt,
    Guid? MovieId,
    string? MovieTitle,
    string? Detail);

public sealed record PageQuery(int? Page = null, int? PageSize = null);

/// <summary><c>Any</c>: at least one of the genres, <c>All</c>: every one of them.</summary>
public enum GenreMatch
{
    Any,
    All,
}

/// <param name="GenreIds">Only movies with these genres; all movies if empty.</param>
/// <param name="GenreMatch">How <c>GenreIds</c> are combined. Default: <c>Any</c>.</param>
/// <param name="ReleasedFrom">Only movies released on or after this day ("YYYY-MM-DD"); movies without release date are left out.</param>
/// <param name="ReleasedTo">Only movies released on or before this day ("YYYY-MM-DD"); movies without release date are left out.</param>
/// <param name="AddedFrom">Only movies put on the watchlist on or after this day ("YYYY-MM-DD", UTC).</param>
/// <param name="AddedTo">Only movies put on the watchlist on or before this day ("YYYY-MM-DD", UTC).</param>
/// <param name="RuntimeFrom">Only movies that are at least this many minutes long; movies without runtime are left out.</param>
/// <param name="RuntimeTo">Only movies that are at most this many minutes long; movies without runtime are left out.</param>
/// <param name="Search">Only movies whose title or original title contains this text.</param>
/// <param name="Tag">Only movies with this tag (case does not matter).</param>
public sealed record WatchlistQuery(
    IReadOnlyList<int>? GenreIds = null,
    GenreMatch? GenreMatch = null,
    string? ReleasedFrom = null,
    string? ReleasedTo = null,
    string? AddedFrom = null,
    string? AddedTo = null,
    int? RuntimeFrom = null,
    int? RuntimeTo = null,
    string? Search = null,
    string? Tag = null,
    int? Page = null,
    int? PageSize = null);

public sealed record User(Guid Id, string Email, string Name, string? PictureUrl);

[JsonConverter(typeof(JsonStringEnumConverter<ImportStatus>))]
public enum ImportStatus
{
    [JsonStringEnumMemberName("running")] Running,
    [JsonStringEnumMemberName("completed")] Completed,
    [JsonStringEnumMemberName("failed")] Failed,
}

/// <summary>A film as Letterboxd names it.</summary>
public sealed record ImportFilm(string Name, int? Year);

/// <param name="DiaryEntriesSkipped">Viewings that were in the diary already (same movie, same day).</param>
/// <param name="WatchlistItemsSkipped">Films that were on the watchlist already.</param>
/// <param name="ManualMovies">Films TMDB does not know (or the server has no TMDB access): added as manual movies.</param>
/// <param name="FailedFilms">Films that could not be looked up because TMDB failed. Not imported; importing again retries them.</param>
/// <param name="Warnings">Rows of the export that were skipped, and why.</param>
public sealed record ImportResult(
    int DiaryEntriesImported,
    int DiaryEntriesSkipped,
    int WatchlistItemsImported,
    int WatchlistItemsSkipped,
    IReadOnlyList<ImportFilm> ManualMovies,
    IReadOnlyList<ImportFilm> FailedFilms,
    IReadOnlyList<string> Warnings);

/// <param name="FilmsTotal">The number of different films in the diary and the watchlist; 0 until the import has started.</param>
public sealed record ImportJob(
    Guid Id,
    ImportStatus Status,
    int FilmsTotal,
    int FilmsProcessed,
    ImportResult? Result,
    string? Error);
