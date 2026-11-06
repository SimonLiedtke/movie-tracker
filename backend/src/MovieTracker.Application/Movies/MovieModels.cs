using MovieTracker.Domain.Diary;
using MovieTracker.Domain.Movies;

namespace MovieTracker.Application.Movies;

/// <summary>A movie as shown in a list of diary entries or watchlist items.</summary>
public sealed record MovieRef(Guid Id, string Title, DateOnly? ReleaseDate, string? PosterUrl);

/// <summary>A movie of the user's library, with the user's own numbers (rating, how often watched, watchlist).</summary>
public sealed record MovieSummary(
    Guid Id,
    string Title,
    DateOnly? ReleaseDate,
    string? PosterUrl,
    IReadOnlyList<Genre> Genres,
    Rating? LatestRating,
    int WatchCount,
    bool OnWatchlist);

public sealed record MovieDetail(
    Guid Id,
    string Title,
    string? OriginalTitle,
    DateOnly? ReleaseDate,
    int? RuntimeMinutes,
    string? Overview,
    string? PosterUrl,
    int? TmdbId,
    IReadOnlyList<Genre> Genres,
    Rating? LatestRating,
    int WatchCount,
    bool OnWatchlist,
    DateOnly? LastWatchedOn,
    DateTimeOffset CreatedAt);

public enum MovieSort
{
    Title,
    TitleDescending,
    ReleaseDate,
    ReleaseDateDescending,
    CreatedAt,
    CreatedAtDescending,
}

/// <param name="Search">Full-text search in title and original title.</param>
/// <param name="Year">Release year.</param>
public sealed record MovieListQuery(string? Search, int? GenreId, int? Year, MovieSort Sort, int Page, int PageSize);

/// <summary>
/// One search hit: either one of the user's manually added movies (<see cref="MovieId"/> set, no <see cref="TmdbId"/>)
/// or a movie from TMDB (<see cref="TmdbId"/> set; <see cref="MovieId"/> only if it was imported before).
/// </summary>
public sealed record MovieSearchHit(
    Guid? MovieId,
    int? TmdbId,
    string Title,
    string? OriginalTitle,
    DateOnly? ReleaseDate,
    string? PosterUrl);

/// <param name="TmdbAvailable">False if TMDB is not configured or could not be reached; the hits are then only manual movies.</param>
public sealed record MovieSearchOutcome(IReadOnlyList<MovieSearchHit> Hits, bool TmdbAvailable);

public enum ImportTmdbMovieStatus
{
    /// <summary>The movie was saved now.</summary>
    Imported,

    /// <summary>The movie was saved before (by anybody).</summary>
    AlreadyImported,

    TmdbNotConfigured,
    TmdbUnavailable,
    UnknownTmdbMovie,
}

public sealed record ImportTmdbMovieResult(ImportTmdbMovieStatus Status, MovieDetail? Movie = null);

public enum SaveMovieStatus
{
    Saved,

    /// <summary>The movie does not exist, or belongs to another user.</summary>
    NotFound,

    /// <summary>Movies from TMDB cannot be changed.</summary>
    ReadOnly,

    UnknownGenres,
}

/// <param name="UnknownGenreIds">Set if <see cref="Status"/> is <see cref="SaveMovieStatus.UnknownGenres"/>.</param>
public sealed record SaveMovieResult(
    SaveMovieStatus Status, MovieDetail? Movie = null, IReadOnlyList<int>? UnknownGenreIds = null);

public enum DeleteMovieResult
{
    Deleted,
    NotFound,

    /// <summary>Movies from TMDB cannot be deleted.</summary>
    ReadOnly,

    /// <summary>The movie is in the diary, which must not lose its history by accident.</summary>
    HasDiaryEntries,
}
