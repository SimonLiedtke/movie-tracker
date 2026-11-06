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
    [HttpGet(Name = "listWatchlist")]
    [ProducesResponseType<PagedResponse<WatchlistItemResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, MediaTypeNames.Application.ProblemJson)]
    public async Task<ActionResult<PagedResponse<WatchlistItemResponse>>> ListWatchlist(
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery, Range(1, 100)] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await watchlist.ListAsync(User.GetUserId(), page, pageSize, cancellationToken);
        return result.ToResponse(item => item.ToResponse());
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
