using System.ComponentModel.DataAnnotations;
using System.Net.Mime;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MovieTracker.Api.Auth;
using MovieTracker.Api.Contracts;
using MovieTracker.Api.Queries;
using MovieTracker.Domain.Diary;
using MovieTracker.Infrastructure.Persistence;

namespace MovieTracker.Api.Controllers;

[ApiController]
[Route("api/diary-entries")]
[Tags("Diary")]
public sealed class DiaryEntriesController(MovieTrackerDbContext db, TimeProvider timeProvider) : ControllerBase
{
    /// <summary>Only the signed-in user's entries. Other users' entries are answered with 404, as if they did not exist.</summary>
    private IQueryable<DiaryEntry> OwnEntries
    {
        get
        {
            var userId = User.GetUserId();
            return db.DiaryEntries.Where(entry => entry.UserId == userId);
        }
    }

    [HttpGet(Name = "listDiaryEntries")]
    [ProducesResponseType<PagedResponse<DiaryEntryResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, MediaTypeNames.Application.ProblemJson)]
    public async Task<ActionResult<PagedResponse<DiaryEntryResponse>>> ListDiaryEntries(
        [FromQuery(Name = "from")] DateOnly? fromDate,
        [FromQuery(Name = "to")] DateOnly? toDate,
        [FromQuery] Guid? movieId,
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery, Range(1, 100)] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (fromDate > toDate)
        {
            ModelState.AddModelError("from", "'from' must not be after 'to'.");
            return ValidationProblem(ModelState);
        }

        var entries = OwnEntries.AsNoTracking();

        if (fromDate is not null)
        {
            entries = entries.Where(entry => entry.WatchedOn >= fromDate);
        }

        if (toDate is not null)
        {
            entries = entries.Where(entry => entry.WatchedOn <= toDate);
        }

        if (movieId is not null)
        {
            entries = entries.Where(entry => entry.MovieId == movieId);
        }

        var totalCount = await entries.CountAsync(cancellationToken);
        var rows = await entries
            .OrderByNewest()
            .Paginate(page, pageSize)
            .SelectRows()
            .ToListAsync(cancellationToken);

        return new PagedResponse<DiaryEntryResponse>(
            rows.Select(row => row.ToResponse()).ToList(), page, pageSize, totalCount);
    }

    [HttpGet("{entryId:guid}", Name = "getDiaryEntry")]
    [ProducesResponseType<DiaryEntryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, MediaTypeNames.Application.ProblemJson)]
    public async Task<ActionResult<DiaryEntryResponse>> GetDiaryEntry(Guid entryId, CancellationToken cancellationToken)
    {
        var response = await FindResponseAsync(entryId, cancellationToken);
        if (response is null)
        {
            return NotFound();
        }

        return response;
    }

    [HttpPost(Name = "createDiaryEntry")]
    [ProducesResponseType<DiaryEntryResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, MediaTypeNames.Application.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, MediaTypeNames.Application.ProblemJson)]
    public async Task<ActionResult<DiaryEntryResponse>> CreateDiaryEntry(
        DiaryEntryRequest request, CancellationToken cancellationToken)
    {
        if (IsInFuture(request.WatchedOn!.Value))
        {
            ModelState.AddModelError("watchedOn", "The viewing date must not be in the future.");
            return ValidationProblem(ModelState);
        }

        if (!await db.Movies.VisibleTo(User.GetUserId()).AnyAsync(movie => movie.Id == request.MovieId, cancellationToken))
        {
            return UnknownMovie(request.MovieId!.Value);
        }

        var entry = new DiaryEntry(User.GetUserId(), request.ToDetails(), timeProvider.GetUtcNow());
        db.DiaryEntries.Add(entry);
        await db.SaveChangesAsync(cancellationToken);

        var response = await FindResponseAsync(entry.Id, cancellationToken);
        return CreatedAtRoute("getDiaryEntry", new { entryId = entry.Id }, response);
    }

    [HttpPut("{entryId:guid}", Name = "updateDiaryEntry")]
    [ProducesResponseType<DiaryEntryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, MediaTypeNames.Application.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, MediaTypeNames.Application.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, MediaTypeNames.Application.ProblemJson)]
    public async Task<ActionResult<DiaryEntryResponse>> UpdateDiaryEntry(
        Guid entryId, DiaryEntryRequest request, CancellationToken cancellationToken)
    {
        var entry = await OwnEntries.SingleOrDefaultAsync(e => e.Id == entryId, cancellationToken);
        if (entry is null)
        {
            return NotFound();
        }

        if (IsInFuture(request.WatchedOn!.Value))
        {
            ModelState.AddModelError("watchedOn", "The viewing date must not be in the future.");
            return ValidationProblem(ModelState);
        }

        if (!await db.Movies.VisibleTo(User.GetUserId()).AnyAsync(movie => movie.Id == request.MovieId, cancellationToken))
        {
            return UnknownMovie(request.MovieId!.Value);
        }

        entry.Update(request.ToDetails());
        await db.SaveChangesAsync(cancellationToken);

        return (await FindResponseAsync(entryId, cancellationToken))!;
    }

    [HttpDelete("{entryId:guid}", Name = "deleteDiaryEntry")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, MediaTypeNames.Application.ProblemJson)]
    public async Task<IActionResult> DeleteDiaryEntry(Guid entryId, CancellationToken cancellationToken)
    {
        var deleted = await OwnEntries
            .Where(entry => entry.Id == entryId)
            .ExecuteDeleteAsync(cancellationToken);

        if (deleted == 0)
        {
            return NotFound();
        }

        return NoContent();
    }

    private async Task<DiaryEntryResponse?> FindResponseAsync(Guid entryId, CancellationToken cancellationToken)
    {
        var row = await OwnEntries
            .AsNoTracking()
            .Where(entry => entry.Id == entryId)
            .SelectRows()
            .SingleOrDefaultAsync(cancellationToken);

        return row?.ToResponse();
    }

    private bool IsInFuture(DateOnly watchedOn)
    {
        // One day of tolerance: the client's local date can already be ahead of the UTC date.
        var latestAllowed = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime).AddDays(1);
        return watchedOn > latestAllowed;
    }

    private ObjectResult UnknownMovie(Guid movieId) => Problem(
        statusCode: StatusCodes.Status422UnprocessableEntity,
        title: "Unknown movie",
        detail: $"No movie with ID {movieId} exists.");
}
