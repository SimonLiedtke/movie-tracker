using System.ComponentModel.DataAnnotations;
using System.Net.Mime;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MovieTracker.Api.Contracts;
using MovieTracker.Api.Queries;
using MovieTracker.Domain.Movies;
using MovieTracker.Infrastructure.Persistence;

namespace MovieTracker.Api.Controllers;

[ApiController]
[Route("api/movies")]
[Tags("Movies")]
public sealed class MoviesController(MovieTrackerDbContext db, TimeProvider timeProvider) : ControllerBase
{
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
        var movies = db.Movies.AsNoTracking().Search(search);

        if (genreId is not null)
        {
            movies = movies.Where(movie => movie.Genres.Any(genre => genre.Id == genreId));
        }

        if (year is not null)
        {
            // Range comparison instead of ReleaseDate.Year, so an index on release_date could be used.
            var firstDay = new DateOnly(year.Value, 1, 1);
            var firstDayOfNextYear = firstDay.AddYears(1);
            movies = movies.Where(movie => movie.ReleaseDate >= firstDay && movie.ReleaseDate < firstDayOfNextYear);
        }

        var ordered = sort switch
        {
            "-title" => movies.OrderByDescending(movie => movie.Title),
            "releaseDate" => movies.OrderBy(movie => movie.ReleaseDate),
            "-releaseDate" => movies.OrderByDescending(movie => movie.ReleaseDate),
            "createdAt" => movies.OrderBy(movie => movie.CreatedAt),
            "-createdAt" => movies.OrderByDescending(movie => movie.CreatedAt),
            _ => movies.OrderBy(movie => movie.Title),
        };

        var totalCount = await movies.CountAsync(cancellationToken);
        var rows = await ordered
            .ThenBy(movie => movie.Id) // stable order for paging
            .Paginate(page, pageSize)
            .SelectSummaryRows(db)
            .ToListAsync(cancellationToken);

        return new PagedResponse<MovieSummaryResponse>(
            rows.Select(row => row.ToResponse()).ToList(), page, pageSize, totalCount);
    }

    [HttpGet("{movieId:guid}", Name = "getMovie")]
    [ProducesResponseType<MovieResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, MediaTypeNames.Application.ProblemJson)]
    public async Task<ActionResult<MovieResponse>> GetMovie(Guid movieId, CancellationToken cancellationToken)
    {
        var response = await FindMovieResponseAsync(movieId, cancellationToken);
        if (response is null)
        {
            return NotFound();
        }

        return response;
    }

    [HttpPost(Name = "createMovie")]
    [ProducesResponseType<MovieResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, MediaTypeNames.Application.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, MediaTypeNames.Application.ProblemJson)]
    public async Task<ActionResult<MovieResponse>> CreateMovie(
        MovieRequest request, CancellationToken cancellationToken)
    {
        var genres = await LoadGenresAsync(request.GenreIds!, cancellationToken);
        if (genres is null)
        {
            return ValidationProblem(ModelState);
        }

        if (await IsTmdbIdTakenAsync(request.TmdbId, excludedMovieId: null, cancellationToken))
        {
            return TmdbIdConflict(request.TmdbId!.Value);
        }

        var movie = new Movie(request.ToDetails(), genres, timeProvider.GetUtcNow());
        db.Movies.Add(movie);
        await db.SaveChangesAsync(cancellationToken);

        var response = await FindMovieResponseAsync(movie.Id, cancellationToken);
        return CreatedAtRoute("getMovie", new { movieId = movie.Id }, response);
    }

    [HttpPut("{movieId:guid}", Name = "updateMovie")]
    [ProducesResponseType<MovieResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, MediaTypeNames.Application.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, MediaTypeNames.Application.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, MediaTypeNames.Application.ProblemJson)]
    public async Task<ActionResult<MovieResponse>> UpdateMovie(
        Guid movieId, MovieRequest request, CancellationToken cancellationToken)
    {
        var movie = await db.Movies
            .Include(m => m.Genres)
            .SingleOrDefaultAsync(m => m.Id == movieId, cancellationToken);
        if (movie is null)
        {
            return NotFound();
        }

        var genres = await LoadGenresAsync(request.GenreIds!, cancellationToken);
        if (genres is null)
        {
            return ValidationProblem(ModelState);
        }

        if (await IsTmdbIdTakenAsync(request.TmdbId, excludedMovieId: movieId, cancellationToken))
        {
            return TmdbIdConflict(request.TmdbId!.Value);
        }

        movie.Update(request.ToDetails(), genres);
        await db.SaveChangesAsync(cancellationToken);

        return (await FindMovieResponseAsync(movieId, cancellationToken))!;
    }

    [HttpDelete("{movieId:guid}", Name = "deleteMovie")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, MediaTypeNames.Application.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, MediaTypeNames.Application.ProblemJson)]
    public async Task<IActionResult> DeleteMovie(Guid movieId, CancellationToken cancellationToken)
    {
        if (!await db.Movies.AnyAsync(movie => movie.Id == movieId, cancellationToken))
        {
            return NotFound();
        }

        if (await db.DiaryEntries.AnyAsync(entry => entry.MovieId == movieId, cancellationToken))
        {
            return Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Movie has diary entries",
                detail: "Delete the diary entries of this movie before deleting the movie.");
        }

        // Watchlist entry and genre assignments are removed by ON DELETE CASCADE in the database.
        await db.Movies.Where(movie => movie.Id == movieId).ExecuteDeleteAsync(cancellationToken);
        return NoContent();
    }

    [HttpGet("{movieId:guid}/diary-entries", Name = "listMovieDiaryEntries")]
    [ProducesResponseType<List<DiaryEntryResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, MediaTypeNames.Application.ProblemJson)]
    public async Task<ActionResult<List<DiaryEntryResponse>>> ListMovieDiaryEntries(
        Guid movieId, CancellationToken cancellationToken)
    {
        if (!await db.Movies.AnyAsync(movie => movie.Id == movieId, cancellationToken))
        {
            return NotFound();
        }

        var rows = await db.DiaryEntries
            .AsNoTracking()
            .Where(entry => entry.MovieId == movieId)
            .OrderByNewest()
            .SelectRows()
            .ToListAsync(cancellationToken);

        return rows.Select(row => row.ToResponse()).ToList();
    }

    private async Task<MovieResponse?> FindMovieResponseAsync(Guid movieId, CancellationToken cancellationToken)
    {
        var row = await db.Movies
            .AsNoTracking()
            .Where(movie => movie.Id == movieId)
            .SelectDetailRows(db)
            .SingleOrDefaultAsync(cancellationToken);

        return row?.ToResponse();
    }

    /// <summary>
    /// Loads the requested genres. Returns null and adds a model error if any ID is unknown.
    /// </summary>
    private async Task<List<Genre>?> LoadGenresAsync(IEnumerable<int> genreIds, CancellationToken cancellationToken)
    {
        var requestedIds = genreIds.Distinct().ToList();
        var genres = await db.Genres
            .Where(genre => requestedIds.Contains(genre.Id))
            .ToListAsync(cancellationToken);

        var unknownIds = requestedIds.Except(genres.Select(genre => genre.Id)).ToList();
        if (unknownIds.Count == 0)
        {
            return genres;
        }

        ModelState.AddModelError("genreIds", $"Unknown genre IDs: {string.Join(", ", unknownIds)}.");
        return null;
    }

    private async Task<bool> IsTmdbIdTakenAsync(int? tmdbId, Guid? excludedMovieId, CancellationToken cancellationToken)
    {
        if (tmdbId is null)
        {
            return false;
        }

        return await db.Movies.AnyAsync(
            movie => movie.TmdbId == tmdbId && movie.Id != excludedMovieId,
            cancellationToken);
    }

    private ObjectResult TmdbIdConflict(int tmdbId) => Problem(
        statusCode: StatusCodes.Status409Conflict,
        title: "Duplicate TMDB ID",
        detail: $"Another movie with TMDB ID {tmdbId} already exists.");
}
