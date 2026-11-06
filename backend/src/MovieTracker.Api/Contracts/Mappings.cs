using MovieTracker.Application;
using MovieTracker.Application.Diary;
using MovieTracker.Application.Movies;
using MovieTracker.Application.Watchlist;
using MovieTracker.Domain.Movies;

namespace MovieTracker.Api.Contracts;

/// <summary>Maps what the Application layer returns to the responses of the API.</summary>
internal static class Mappings
{
    public static PagedResponse<TResponse> ToResponse<TItem, TResponse>(
        this PagedResult<TItem> result, Func<TItem, TResponse> map) =>
        new(result.Items.Select(map).ToList(), result.Page, result.PageSize, result.TotalCount);

    public static GenreResponse ToResponse(this Genre genre) => new(genre.Id, genre.Name);

    public static MovieRefResponse ToResponse(this MovieRef movie) => new(movie.Id, movie.Title, movie.ReleaseDate, movie.PosterUrl);

    public static DiaryEntryResponse ToResponse(this DiaryEntryView entry) => new(
        entry.Id,
        entry.Movie.ToResponse(),
        entry.WatchedOn,
        entry.Rating?.HalfStars,
        entry.Review,
        entry.IsRewatch,
        entry.Liked,
        entry.CreatedAt);

    public static WatchlistItemResponse ToResponse(this WatchlistItemView item) => new(item.Movie.ToResponse(), item.AddedAt, item.Note);

    public static MovieSummaryResponse ToResponse(this MovieSummary movie) => new(
        movie.Id,
        movie.Title,
        movie.ReleaseDate,
        movie.PosterUrl,
        movie.Genres.Select(ToResponse).ToList(),
        movie.LatestRating?.HalfStars,
        movie.WatchCount,
        movie.OnWatchlist);

    public static MovieResponse ToResponse(this MovieDetail movie) => new(
        movie.Id,
        movie.Title,
        movie.OriginalTitle,
        movie.ReleaseDate,
        movie.RuntimeMinutes,
        movie.Overview,
        movie.PosterUrl,
        movie.TmdbId,
        movie.Genres.Select(ToResponse).ToList(),
        movie.LatestRating?.HalfStars,
        movie.WatchCount,
        movie.OnWatchlist,
        movie.LastWatchedOn,
        movie.CreatedAt);

    public static MovieSearchResult ToResponse(this MovieSearchHit hit) => new(
        hit.MovieId, hit.TmdbId, hit.Title, hit.OriginalTitle, hit.ReleaseDate, hit.PosterUrl);
}
