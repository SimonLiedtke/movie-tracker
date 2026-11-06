using System.ComponentModel.DataAnnotations;
using System.Net.Mime;
using Microsoft.AspNetCore.Mvc;
using MovieTracker.Api.Auth;
using MovieTracker.Api.Contracts;
using MovieTracker.Application.Diary;

namespace MovieTracker.Api.Controllers;

[ApiController]
[Route("api/diary-entries")]
[Tags("Diary")]
public sealed class DiaryEntriesController(DiaryService diary) : ControllerBase
{
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

        var result = await diary.ListAsync(
            User.GetUserId(), new DiaryFilter(fromDate, toDate, movieId), page, pageSize, cancellationToken);
        return result.ToResponse(entry => entry.ToResponse());
    }

    [HttpGet("{entryId:guid}", Name = "getDiaryEntry")]
    [ProducesResponseType<DiaryEntryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, MediaTypeNames.Application.ProblemJson)]
    public async Task<ActionResult<DiaryEntryResponse>> GetDiaryEntry(Guid entryId, CancellationToken cancellationToken)
    {
        var entry = await diary.GetAsync(User.GetUserId(), entryId, cancellationToken);
        if (entry is null)
        {
            return NotFound();
        }

        return entry.ToResponse();
    }

    [HttpPost(Name = "createDiaryEntry")]
    [ProducesResponseType<DiaryEntryResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, MediaTypeNames.Application.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, MediaTypeNames.Application.ProblemJson)]
    public async Task<ActionResult<DiaryEntryResponse>> CreateDiaryEntry(
        DiaryEntryRequest request, CancellationToken cancellationToken)
    {
        var result = await diary.CreateAsync(User.GetUserId(), request.ToDetails(), cancellationToken);
        if (result.Status != SaveDiaryEntryStatus.Saved)
        {
            return Failure(result.Status, request);
        }

        var response = result.Entry!.ToResponse();
        return CreatedAtRoute("getDiaryEntry", new { entryId = response.Id }, response);
    }

    [HttpPut("{entryId:guid}", Name = "updateDiaryEntry")]
    [ProducesResponseType<DiaryEntryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, MediaTypeNames.Application.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, MediaTypeNames.Application.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, MediaTypeNames.Application.ProblemJson)]
    public async Task<ActionResult<DiaryEntryResponse>> UpdateDiaryEntry(
        Guid entryId, DiaryEntryRequest request, CancellationToken cancellationToken)
    {
        var result = await diary.UpdateAsync(User.GetUserId(), entryId, request.ToDetails(), cancellationToken);
        if (result.Status != SaveDiaryEntryStatus.Saved)
        {
            return Failure(result.Status, request);
        }

        return result.Entry!.ToResponse();
    }

    [HttpDelete("{entryId:guid}", Name = "deleteDiaryEntry")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, MediaTypeNames.Application.ProblemJson)]
    public async Task<IActionResult> DeleteDiaryEntry(Guid entryId, CancellationToken cancellationToken)
    {
        if (!await diary.DeleteAsync(User.GetUserId(), entryId, cancellationToken))
        {
            return NotFound();
        }

        return NoContent();
    }

    private ActionResult Failure(SaveDiaryEntryStatus status, DiaryEntryRequest request)
    {
        switch (status)
        {
            case SaveDiaryEntryStatus.NotFound:
                return NotFound();
            case SaveDiaryEntryStatus.FutureDate:
                ModelState.AddModelError("watchedOn", "The viewing date must not be in the future.");
                return ValidationProblem(ModelState);
            default:
                return Problem(
                    statusCode: StatusCodes.Status422UnprocessableEntity,
                    title: "Unknown movie",
                    detail: $"No movie with ID {request.MovieId} exists.");
        }
    }
}
