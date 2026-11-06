using MovieTracker.Domain.Movies;

namespace MovieTracker.Api.Queries;

/// <summary>Temporary: used by the controllers that do not use the Application layer yet.</summary>
internal static class MovieQueries
{
    /// <summary>Movies from TMDB and the user's own manually added movies. Other users' movies do not exist for them.</summary>
    public static IQueryable<Movie> VisibleTo(this IQueryable<Movie> movies, Guid userId) =>
        movies.Where(movie => movie.OwnerId == null || movie.OwnerId == userId);
}
