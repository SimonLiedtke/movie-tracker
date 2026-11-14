using System.ComponentModel.DataAnnotations;
using System.Net.Mime;
using Microsoft.AspNetCore.Mvc;
using MovieTracker.Api.Auth;
using MovieTracker.Api.Contracts;
using MovieTracker.Application.Diary;
using MovieTracker.Domain.Diary;
using MovieTracker.Domain.Tags;

namespace MovieTracker.Api.Controllers;

[ApiController]
[Route("api/diary-entries")]
[Tags("Diary")]
public sealed class DiaryEntriesController(DiaryService diary) : ControllerBase
{
    /// <summary>
    /// With <paramref name="tag"/>, only entries with this tag (case does not matter). With <paramref name="ratingFrom"/>
    /// and/or <paramref name="ratingTo"/> (in half stars), only entries rated in this range; unrated entries are left out.
    /// </summary>
    [HttpGet(Name = "listDiaryEntries")]
    [ProducesResponseType<PagedResponse<DiaryEntryResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, MediaTypeNames.Application.ProblemJson)]
    public async Task<ActionResult<PagedResponse<DiaryEntryResponse>>> ListDiaryEntries(
        [FromQuery(Name = "from")] DateOnly? fromDate,
        [FromQuery(Name = "to")] DateOnly? toDate,
        [FromQuery] Guid? movieId,
        [FromQuery, StringLength(TagList.MaxTagLength)] string? tag = null,
        [FromQuery, Range(Rating.MinHalfStars, Rating.MaxHalfStars)] int? ratingFrom = null,
        [FromQuery, Range(Rating.MinHalfStars, Rating.MaxHalfStars)] int? ratingTo = null,
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery, Range(1, 100)] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (fromDate > toDate)
        {
            ModelState.AddModelError("from", "'from' must not be after 'to'.");
            return ValidationProblem(ModelState);
        }

        if (ratingFrom > ratingTo)
        {
            ModelState.AddModelError("ratingFrom", "'ratingFrom' must not be greater than 'ratingTo'.");
            return ValidationProblem(ModelState);
        }

        var filter = new DiaryFilter(
            fromDate,
            toDate,
            movieId,
            ToFilterTag(tag),
            ratingFrom is { } lowest ? new Rating(lowest) : null,
            ratingTo is { } highest ? new Rating(highest) : null);
        var result = await diary.ListAsync(User.GetUserId(), filter, page, pageSize, cancellationToken);
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

    /// <summary>The tag in its stored form; null if there is none to look for.</summary>
    private static string? ToFilterTag(string? tag) => TagList.NormalizeTag(tag) is { Length: > 0 } normalized ? normalized : null;

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
