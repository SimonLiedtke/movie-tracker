using Microsoft.EntityFrameworkCore;
using MovieTracker.Application.Genres;
using MovieTracker.Domain.Movies;

namespace MovieTracker.Infrastructure.Persistence.Repositories;

internal sealed class GenreRepository(MovieTrackerDbContext db) : IGenreRepository
{
    public async Task<IReadOnlyList<Genre>> ListAsync(CancellationToken cancellationToken) =>
        await db.Genres.AsNoTracking().OrderBy(genre => genre.Name).ToListAsync(cancellationToken);
}
