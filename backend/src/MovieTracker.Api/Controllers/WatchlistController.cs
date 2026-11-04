using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using System.Net.Mime;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;
using MovieTracker.Api.Auth;
using MovieTracker.Api.Contracts;
using MovieTracker.Api.Queries;
using MovieTracker.Domain.Watchlist;
using MovieTracker.Infrastructure.Persistence;

namespace MovieTracker.Api.Controllers;

[ApiController]
[Route("api/watchlist")]
[Tags("Watchlist")]
public sealed class WatchlistController(MovieTrackerDbContext db, TimeProvider timeProvider) : ControllerBase
{
    /// <summary>Every user has their own watchlist.</summary>
    private IQueryable<WatchlistItem> OwnItems
    {
        get
        {
            var userId = User.GetUserId();
            return db.WatchlistItems.Where(item => item.UserId == userId);
        }
    }

    [HttpGet(Name = "listWatchlist")]
    [ProducesResponseType<PagedResponse<WatchlistItemResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, MediaTypeNames.Application.ProblemJson)]
    public async Task<ActionResult<PagedResponse<WatchlistItemResponse>>> ListWatchlist(
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery, Range(1, 100)] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var items = OwnItems.AsNoTracking();

        var totalCount = await items.CountAsync(cancellationToken);
        var responses = await items
            .OrderByDescending(item => item.AddedAt)
            .ThenBy(item => item.MovieId)
            .Paginate(page, pageSize)
            .Select(ToResponse)
            .ToListAsync(cancellationToken);

        return new PagedResponse<WatchlistItemResponse>(responses, page, pageSize, totalCount);
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
        var item = await db.WatchlistItems.FindAsync([User.GetUserId(), movieId], cancellationToken);

        if (item is not null)
        {
            item.UpdateNote(request?.Note);
            await db.SaveChangesAsync(cancellationToken);
            return await FindResponseAsync(movieId, cancellationToken);
        }

        if (!await db.Movies.AnyAsync(movie => movie.Id == movieId, cancellationToken))
        {
            return NotFound();
        }

        db.WatchlistItems.Add(new WatchlistItem(User.GetUserId(), movieId, request?.Note, timeProvider.GetUtcNow()));
        await db.SaveChangesAsync(cancellationToken);

        return StatusCode(StatusCodes.Status201Created, await FindResponseAsync(movieId, cancellationToken));
    }

    [HttpDelete("{movieId:guid}", Name = "deleteWatchlistItem")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, MediaTypeNames.Application.ProblemJson)]
    public async Task<IActionResult> DeleteWatchlistItem(Guid movieId, CancellationToken cancellationToken)
    {
        var deleted = await OwnItems
            .Where(item => item.MovieId == movieId)
            .ExecuteDeleteAsync(cancellationToken);

        if (deleted == 0)
        {
            return NotFound();
        }

        return NoContent();
    }

    private async Task<WatchlistItemResponse> FindResponseAsync(Guid movieId, CancellationToken cancellationToken) =>
        await OwnItems
            .AsNoTracking()
            .Where(item => item.MovieId == movieId)
            .Select(ToResponse)
            .SingleAsync(cancellationToken);

    private static readonly Expression<Func<WatchlistItem, WatchlistItemResponse>> ToResponse =
        item => new WatchlistItemResponse(
            new MovieRefResponse(item.Movie.Id, item.Movie.Title, item.Movie.ReleaseDate, item.Movie.PosterUrl),
            item.AddedAt,
            item.Note);
}
