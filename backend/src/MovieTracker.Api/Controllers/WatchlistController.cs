using System.ComponentModel.DataAnnotations;
using System.Net.Mime;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using MovieTracker.Api.Auth;
using MovieTracker.Api.Contracts;
using MovieTracker.Application.Watchlist;

namespace MovieTracker.Api.Controllers;

[ApiController]
[Route("api/watchlist")]
[Tags("Watchlist")]
public sealed class WatchlistController(WatchlistService watchlist) : ControllerBase
{
    /// <summary>
    /// With <paramref name="genreId"/> (repeatable), only movies with any (<c>genreMatch=any</c>, the default)
    /// or all (<c>genreMatch=all</c>) of these genres. With <paramref name="releasedFrom"/> and
    /// <paramref name="releasedTo"/>, only movies released in this period, with <paramref name="addedFrom"/> and
    /// <paramref name="addedTo"/>, only items added in this period (both ends included, UTC days).
    /// With <paramref name="runtimeFrom"/> and <paramref name="runtimeTo"/>, only movies that are this many minutes
    /// long (both ends included); movies without a runtime are left out.
    /// </summary>
    [HttpGet(Name = "listWatchlist")]
    [ProducesResponseType<WatchlistPageResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, MediaTypeNames.Application.ProblemJson)]
    public async Task<ActionResult<WatchlistPageResponse>> ListWatchlist(
        [FromQuery] int[]? genreId,
        [FromQuery, AllowedValues("any", "all")] string genreMatch = "any",
        [FromQuery] DateOnly? releasedFrom = null,
        [FromQuery] DateOnly? releasedTo = null,
        [FromQuery] DateOnly? addedFrom = null,
        [FromQuery] DateOnly? addedTo = null,
        [FromQuery, Range(1, 1500)] int? runtimeFrom = null,
        [FromQuery, Range(1, 1500)] int? runtimeTo = null,
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery, Range(1, 100)] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (releasedFrom > releasedTo)
        {
            ModelState.AddModelError("releasedFrom", "'releasedFrom' must not be after 'releasedTo'.");
            return ValidationProblem(ModelState);
        }

        if (addedFrom > addedTo)
        {
            ModelState.AddModelError("addedFrom", "'addedFrom' must not be after 'addedTo'.");
            return ValidationProblem(ModelState);
        }

        if (runtimeFrom > runtimeTo)
        {
            ModelState.AddModelError("runtimeFrom", "'runtimeFrom' must not be greater than 'runtimeTo'.");
            return ValidationProblem(ModelState);
        }

        var filter = new WatchlistFilter(
            genreId ?? [],
            genreMatch == "all" ? GenreMatch.All : GenreMatch.Any,
            releasedFrom,
            releasedTo,
            addedFrom,
            addedTo,
            runtimeFrom,
            runtimeTo);
        var result = await watchlist.ListAsync(User.GetUserId(), filter, page, pageSize, cancellationToken);
        return result.ToResponse();
    }

    [HttpPut("{movieId:guid}", Name = "putWatchlistItem")]
    [ProducesResponseType<WatchlistItemResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<WatchlistItemResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, MediaTypeNames.Application.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, MediaTypeNames.Application.ProblemJson)]
    public async Task<ActionResult<WatchlistItemResponse>> PutWatchlistItem(
        Guid movieId,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] WatchlistItemRequest? request,
        CancellationToken cancellationToken)
    {
        var result = await watchlist.PutAsync(User.GetUserId(), movieId, request?.Note, cancellationToken);

        return result.Status switch
        {
            PutWatchlistItemStatus.NotFound => NotFound(),
            PutWatchlistItemStatus.Added => StatusCode(StatusCodes.Status201Created, result.Item!.ToResponse()),
            _ => result.Item!.ToResponse(),
        };
    }

    [HttpDelete("{movieId:guid}", Name = "deleteWatchlistItem")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, MediaTypeNames.Application.ProblemJson)]
    public async Task<IActionResult> DeleteWatchlistItem(Guid movieId, CancellationToken cancellationToken)
    {
        if (!await watchlist.RemoveAsync(User.GetUserId(), movieId, cancellationToken))
        {
            return NotFound();
        }

        return NoContent();
    }
}
