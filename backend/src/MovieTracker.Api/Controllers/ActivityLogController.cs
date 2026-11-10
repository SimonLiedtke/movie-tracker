using System.ComponentModel.DataAnnotations;
using System.Net.Mime;
using Microsoft.AspNetCore.Mvc;
using MovieTracker.Api.Auth;
using MovieTracker.Api.Contracts;
using MovieTracker.Application.Activity;

namespace MovieTracker.Api.Controllers;

[ApiController]
[Route("api/activity-log")]
[Tags("Activity log")]
public sealed class ActivityLogController(ActivityLogService activityLog) : ControllerBase
{
    /// <summary>
    /// What the signed-in user did that modified the database (logging a viewing, changing a note, removing a movie
    /// from the watchlist, ...), most recent first.
    /// </summary>
    [HttpGet(Name = "listActivityLog")]
    [ProducesResponseType<PagedResponse<ActivityLogEntryResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, MediaTypeNames.Application.ProblemJson)]
    public async Task<ActionResult<PagedResponse<ActivityLogEntryResponse>>> ListActivityLog(
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery, Range(1, 100)] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await activityLog.ListAsync(User.GetUserId(), page, pageSize, cancellationToken);
        return result.ToResponse(entry => entry.ToResponse());
    }
}
