using MovieTracker.Domain.Movies;

namespace MovieTracker.Application.Genres;

public sealed class GenreService(IGenreRepository genres)
{
    public Task<IReadOnlyList<Genre>> ListAsync(CancellationToken cancellationToken) =>
        genres.ListAsync(cancellationToken);
}
