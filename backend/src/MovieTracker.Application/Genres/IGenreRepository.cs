using MovieTracker.Domain.Movies;

namespace MovieTracker.Application.Genres;

public interface IGenreRepository
{
    /// <summary>All genres, ordered by name.</summary>
    Task<IReadOnlyList<Genre>> ListAsync(CancellationToken cancellationToken);
}
