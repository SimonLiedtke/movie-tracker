using Microsoft.AspNetCore.Mvc;
using MovieTracker.Api.Contracts;
using MovieTracker.Application.Genres;

namespace MovieTracker.Api.Controllers;

[ApiController]
[Route("api/genres")]
[Tags("Genres")]
public sealed class GenresController(GenreService genres) : ControllerBase
{
    [HttpGet(Name = "listGenres")]
    [ProducesResponseType<List<GenreResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<GenreResponse>>> ListGenres(CancellationToken cancellationToken) =>
        (await genres.ListAsync(cancellationToken)).Select(genre => new GenreResponse(genre.Id, genre.Name)).ToList();
}
