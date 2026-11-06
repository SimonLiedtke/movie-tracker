using MovieTracker.Domain.Movies;

namespace MovieTracker.Application.Genres;

public interface IGenreRepository
{
    /// <summary>All genres, ordered by name.</summary>
    Task<IReadOnlyList<Genre>> ListAsync(CancellationToken cancellationToken);

    /// <summary>
    /// The genres with these IDs; unknown IDs are left out. For assigning them to a movie, which
    /// <see cref="IUnitOfWork"/> then saves.
    /// </summary>
    Task<IReadOnlyList<Genre>> FindByIdsAsync(IReadOnlyCollection<int> genreIds, CancellationToken cancellationToken);
}
