using System.ComponentModel.DataAnnotations;
using System.Net.Mime;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using MovieTracker.Api.Auth;
using MovieTracker.Api.Contracts;
using MovieTracker.Application.Diary;
using MovieTracker.Application.Movies;
using MovieTracker.Domain.Movies;

namespace MovieTracker.Api.Controllers;

/// <summary>
/// Movies come from TMDB (shared, read-only) or are added manually (private to their owner, editable).
/// Other users' manual movies are answered with 404, as if they did not exist.
/// </summary>
[ApiController]
[Route("api/movies")]
[Tags("Movies")]
public sealed class MoviesController(MovieService movies, DiaryService diary) : ControllerBase
{
    /// <summary>The poster image plus a little for the rest of the multipart form. Bigger requests are rejected with 400.</summary>
    private const int MaxPosterRequestBytes = MoviePoster.MaxBytes + 64 * 1024;

    /// <summary>The user's movies: added manually, logged in the diary or on the watchlist.</summary>
    [HttpGet(Name = "listMovies")]
    [ProducesResponseType<PagedResponse<MovieSummaryResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, MediaTypeNames.Application.ProblemJson)]
    public async Task<ActionResult<PagedResponse<MovieSummaryResponse>>> ListMovies(
        [FromQuery, StringLength(200)] string? search,
        [FromQuery] int? genreId,
        [FromQuery, Range(1870, 2100)] int? year,
        [FromQuery, AllowedValues("title", "-title", "releaseDate", "-releaseDate", "createdAt", "-createdAt")]
        string sort = "title",
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery, Range(1, 100)] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var movieSort = sort switch
        {
            "-title" => MovieSort.TitleDescending,
            "releaseDate" => MovieSort.ReleaseDate,
            "-releaseDate" => MovieSort.ReleaseDateDescending,
            "createdAt" => MovieSort.CreatedAt,
            "-createdAt" => MovieSort.CreatedAtDescending,
            _ => MovieSort.Title,
        };

        var result = await movies.ListAsync(
            User.GetUserId(), new MovieListQuery(search, genreId, year, movieSort, page, pageSize), cancellationToken);
        return result.ToResponse(movie => movie.ToResponse());
    }

    /// <summary>Finds movies to log or watchlist: the user's manually added movies first, then TMDB's results.</summary>
    [HttpGet("search", Name = "searchMovies")]
    [ProducesResponseType<MovieSearchResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, MediaTypeNames.Application.ProblemJson)]
    public async Task<ActionResult<MovieSearchResponse>> SearchMovies(
        [FromQuery, Required, StringLength(200, MinimumLength = 1)] string query,
        CancellationToken cancellationToken)
    {
        var result = await movies.SearchAsync(User.GetUserId(), query, cancellationToken);
        return new MovieSearchResponse(result.Hits.Select(hit => hit.ToResponse()).ToList(), result.TmdbAvailable);
    }

    /// <summary>
    /// Makes a TMDB movie available for diary and watchlist (idempotent): 201 the first time, 200 afterwards.
    /// </summary>
    [HttpPost("tmdb/{tmdbId:int:min(1)}", Name = "importTmdbMovie")]
    [ProducesResponseType<MovieResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<MovieResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, MediaTypeNames.Application.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway, MediaTypeNames.Application.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable, MediaTypeNames.Application.ProblemJson)]
    public async Task<ActionResult<MovieResponse>> ImportTmdbMovie(int tmdbId, CancellationToken cancellationToken)
    {
        var result = await movies.ImportTmdbMovieAsync(User.GetUserId(), tmdbId, cancellationToken);

        switch (result.Status)
        {
            case ImportTmdbMovieStatus.AlreadyImported:
                return result.Movie!.ToResponse();
            case ImportTmdbMovieStatus.Imported:
                return CreatedAtRoute("getMovie", new { movieId = result.Movie!.Id }, result.Movie.ToResponse());
            case ImportTmdbMovieStatus.TmdbNotConfigured:
                return TmdbNotConfigured();
            case ImportTmdbMovieStatus.TmdbUnavailable:
                return Problem(
                    statusCode: StatusCodes.Status502BadGateway,
                    title: "TMDB is unavailable",
                    detail: "The movie could not be loaded from TMDB. Please try again later.");
            default:
                return Problem(
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Unknown TMDB movie",
                    detail: $"TMDB has no movie with ID {tmdbId}.");
        }
    }

    [HttpGet("{movieId:guid}", Name = "getMovie")]
    [ProducesResponseType<MovieResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, MediaTypeNames.Application.ProblemJson)]
    public async Task<ActionResult<MovieResponse>> GetMovie(Guid movieId, CancellationToken cancellationToken)
    {
        var movie = await movies.GetAsync(User.GetUserId(), movieId, cancellationToken);
        if (movie is null)
        {
            return NotFound();
        }

        return movie.ToResponse();
    }

    /// <summary>Adds a movie manually, e.g. one that TMDB does not know. Only the signed-in user sees it.</summary>
    [HttpPost(Name = "createMovie")]
    [ProducesResponseType<MovieResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, MediaTypeNames.Application.ProblemJson)]
    public async Task<ActionResult<MovieResponse>> CreateMovie(
        MovieRequest request, CancellationToken cancellationToken)
    {
        var result = await movies.CreateAsync(User.GetUserId(), request.ToDetails(), request.GenreIds!, cancellationToken);
        if (result.Status == SaveMovieStatus.UnknownGenres)
        {
            return UnknownGenres(result);
        }

        if (result.Status == SaveMovieStatus.InvalidPosterUrl)
        {
            return InvalidPosterUrl();
        }

        var response = result.Movie!.ToResponse();
        return CreatedAtRoute("getMovie", new { movieId = response.Id }, response);
    }

    /// <summary>Only manually added movies can be edited.</summary>
    [HttpPut("{movieId:guid}", Name = "updateMovie")]
    [ProducesResponseType<MovieResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, MediaTypeNames.Application.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, MediaTypeNames.Application.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, MediaTypeNames.Application.ProblemJson)]
    public async Task<ActionResult<MovieResponse>> UpdateMovie(
        Guid movieId, MovieRequest request, CancellationToken cancellationToken)
    {
        var result = await movies.UpdateAsync(
            User.GetUserId(), movieId, request.ToDetails(), request.GenreIds!, cancellationToken);

        return result.Status switch
        {
            SaveMovieStatus.NotFound => NotFound(),
            SaveMovieStatus.ReadOnly => TmdbMovieIsReadOnly(),
            SaveMovieStatus.UnknownGenres => UnknownGenres(result),
            SaveMovieStatus.InvalidPosterUrl => InvalidPosterUrl(),
            _ => result.Movie!.ToResponse(),
        };
    }

    /// <summary>The image of the movie's uploaded poster, for showing it. 404 for movies with an external poster.</summary>
    [HttpGet("{movieId:guid}/poster", Name = "getMoviePoster")]
    [ProducesResponseType<FileContentResult>(StatusCodes.Status200OK, "image/jpeg", "image/png", "image/webp")]
    [ProducesResponseType(StatusCodes.Status304NotModified)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, MediaTypeNames.Application.ProblemJson)]
    public async Task<IActionResult> GetMoviePoster(Guid movieId, CancellationToken cancellationToken)
    {
        var poster = await movies.GetPosterAsync(User.GetUserId(), movieId, cancellationToken);
        if (poster is null)
        {
            return NotFound();
        }

        // The URL stays the same when the poster is replaced, so browsers must ask again (cheap: 304 with the ETag).
        Response.Headers.CacheControl = "private, no-cache";
        Response.Headers.XContentTypeOptions = "nosniff";
        return File(
            poster.Data,
            poster.ContentType,
            poster.UpdatedAt,
            new EntityTagHeaderValue($"\"{poster.UpdatedAt.UtcTicks}\""));
    }

    /// <summary>
    /// Uploads the poster of a manually added movie as image file (JPEG, PNG or WebP, at most 5 MB). It replaces
    /// the movie's previous poster, whether that was an uploaded image or a URL.
    /// </summary>
    [HttpPut("{movieId:guid}/poster", Name = "uploadMoviePoster")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxPosterRequestBytes)]
    [ProducesResponseType<MovieResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, MediaTypeNames.Application.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, MediaTypeNames.Application.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, MediaTypeNames.Application.ProblemJson)]
    public async Task<ActionResult<MovieResponse>> UploadMoviePoster(
        Guid movieId, IFormFile file, CancellationToken cancellationToken)
    {
        if (file.Length is 0 or > MoviePoster.MaxBytes)
        {
            return InvalidPosterImage();
        }

        byte[] image;
        await using (var stream = file.OpenReadStream())
        {
            image = new byte[file.Length];
            await stream.ReadExactlyAsync(image, cancellationToken);
        }

        var result = await movies.SetPosterAsync(User.GetUserId(), movieId, image, cancellationToken);

        return result.Status switch
        {
            SaveMovieStatus.NotFound => NotFound(),
            SaveMovieStatus.ReadOnly => TmdbMovieIsReadOnly(),
            SaveMovieStatus.InvalidPosterImage => InvalidPosterImage(),
            _ => result.Movie!.ToResponse(),
        };
    }

    /// <summary>Only manually added movies can be deleted, and only while they are not in the diary.</summary>
    [HttpDelete("{movieId:guid}", Name = "deleteMovie")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, MediaTypeNames.Application.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, MediaTypeNames.Application.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, MediaTypeNames.Application.ProblemJson)]
    public async Task<IActionResult> DeleteMovie(Guid movieId, CancellationToken cancellationToken)
    {
        var result = await movies.DeleteAsync(User.GetUserId(), movieId, cancellationToken);

        return result switch
        {
            DeleteMovieResult.NotFound => NotFound(),
            DeleteMovieResult.ReadOnly => TmdbMovieIsReadOnly(),
            DeleteMovieResult.HasDiaryEntries => Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Movie has diary entries",
                detail: "Movies that are in your diary cannot be deleted."),
            _ => NoContent(),
        };
    }

    /// <summary>The signed-in user's viewings of the movie, newest first.</summary>
    [HttpGet("{movieId:guid}/diary-entries", Name = "listMovieDiaryEntries")]
    [ProducesResponseType<List<DiaryEntryResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, MediaTypeNames.Application.ProblemJson)]
    public async Task<ActionResult<List<DiaryEntryResponse>>> ListMovieDiaryEntries(
        Guid movieId, CancellationToken cancellationToken)
    {
        var entries = await diary.ListForMovieAsync(User.GetUserId(), movieId, cancellationToken);
        if (entries is null)
        {
            return NotFound();
        }

        return entries.Select(entry => entry.ToResponse()).ToList();
    }

    private ActionResult UnknownGenres(SaveMovieResult result)
    {
        ModelState.AddModelError("genreIds", $"Unknown genre IDs: {string.Join(", ", result.UnknownGenreIds!)}.");
        return ValidationProblem(ModelState);
    }

    private ActionResult InvalidPosterUrl()
    {
        ModelState.AddModelError(
            "posterUrl", "The poster URL must be an http(s) URL, or the movie's own uploaded poster URL.");
        return ValidationProblem(ModelState);
    }

    private ActionResult InvalidPosterImage()
    {
        ModelState.AddModelError("file", "The poster must be a JPEG, PNG or WebP image of at most 5 MB.");
        return ValidationProblem(ModelState);
    }

    private ObjectResult TmdbMovieIsReadOnly() => Problem(
        statusCode: StatusCodes.Status403Forbidden,
        title: "Movie is read-only",
        detail: "Movies from TMDB cannot be edited or deleted. Only manually added movies can.");

    private ObjectResult TmdbNotConfigured() => Problem(
        statusCode: StatusCodes.Status503ServiceUnavailable,
        title: "TMDB is not configured",
        detail: "This server has no TMDB access token, so movies can only be added manually.");
}
