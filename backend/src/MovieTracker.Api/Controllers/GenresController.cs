using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MovieTracker.Api.Contracts;
using MovieTracker.Infrastructure.Persistence;

namespace MovieTracker.Api.Controllers;

[ApiController]
[Route("api/genres")]
[Tags("Genres")]
public sealed class GenresController(MovieTrackerDbContext db) : ControllerBase
{
    [HttpGet(Name = "listGenres")]
    [ProducesResponseType<List<GenreResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<GenreResponse>>> ListGenres(CancellationToken cancellationToken) =>
        await db.Genres
            .AsNoTracking()
            .OrderBy(genre => genre.Name)
            .Select(genre => new GenreResponse(genre.Id, genre.Name))
            .ToListAsync(cancellationToken);
}
