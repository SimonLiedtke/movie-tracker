using System.Net.Mime;
using Microsoft.AspNetCore.Mvc;
using MovieTracker.Api.Auth;
using MovieTracker.Api.Contracts;
using MovieTracker.Api.Imports;
using MovieTracker.Infrastructure.Letterboxd;

namespace MovieTracker.Api.Controllers;

/// <summary>
/// Imports the diary and watchlist from a Letterboxd data export. Looking up hundreds of films on TMDB takes a while, so the
/// upload only starts the import and the client asks for its progress.
/// </summary>
[ApiController]
[Route("api/imports/letterboxd")]
[Tags("Import")]
public sealed class LetterboxdImportsController(LetterboxdImportQueue queue, TimeProvider timeProvider) : ControllerBase
{
    /// <summary>
    /// A Letterboxd export is a few hundred kilobytes, even for thousands of films. Kestrel rejects bigger requests
    /// with 400 while the form is read, before the action runs.
    /// </summary>
    private const int MaxUploadBytes = 10 * 1024 * 1024;

    [HttpPost(Name = "startLetterboxdImport")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxUploadBytes)]
    [ProducesResponseType<ImportJobResponse>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, MediaTypeNames.Application.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, MediaTypeNames.Application.ProblemJson)]
    public ActionResult<ImportJobResponse> StartImport(IFormFile file)
    {
        LetterboxdExport export;
        try
        {
            using var stream = file.OpenReadStream();
            export = LetterboxdExportReader.Read(stream);
        }
        catch (InvalidLetterboxdExportException exception)
        {
            ModelState.AddModelError("file", exception.Message);
            return ValidationProblem(ModelState);
        }

        var job = queue.TryEnqueue(User.GetUserId(), export, timeProvider.GetUtcNow());
        if (job is null)
        {
            return Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Import already running",
                detail: "Your previous import has not finished yet. Please wait until it is done.");
        }

        return AcceptedAtRoute("getLetterboxdImport", new { importId = job.Id }, job.ToResponse());
    }

    /// <summary>Other users' imports are answered with 404, as if they did not exist. So are imports from before a server restart.</summary>
    [HttpGet("{importId:guid}", Name = "getLetterboxdImport")]
    [ProducesResponseType<ImportJobResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, MediaTypeNames.Application.ProblemJson)]
    public ActionResult<ImportJobResponse> GetImport(Guid importId)
    {
        var job = queue.Find(User.GetUserId(), importId);
        if (job is null)
        {
            return NotFound();
        }

        return job.ToResponse();
    }
}
