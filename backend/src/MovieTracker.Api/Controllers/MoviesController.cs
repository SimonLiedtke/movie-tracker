using System.ComponentModel.DataAnnotations;
using System.Net.Mime;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MovieTracker.Api.Auth;
using MovieTracker.Api.Contracts;
using MovieTracker.Api.Queries;
using MovieTracker.Domain.Movies;
using MovieTracker.Infrastructure.Persistence;
using MovieTracker.Infrastructure.Tmdb;
using Npgsql;

namespace MovieTracker.Api.Controllers;

/// <summary>
/// Movies come from TMDB (shared, read-only) or are added manually (private to their owner, editable).
/// Other users' manual movies are answered with 404, as if they did not exist.
/// </summary>
[ApiController]
[Route("api/movies")]
[Tags("Movies")]
public sealed class MoviesController(
    MovieTrackerDbContext db,
    ITmdbClient tmdb,
    TimeProvider timeProvider,
    ILogger<MoviesController> logger) : ControllerBase
{
    private const int MaxManualSearchResults = 10;

    private IQueryable<Movie> VisibleMovies => db.Movies.VisibleTo(User.GetUserId());

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
        var userId = User.GetUserId();
        var movies = db.Movies.AsNoTracking().InLibraryOf(db, userId).Search(search);

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
            .SelectSummaryRows(db, userId)
            .ToListAsync(cancellationToken);

        return new PagedResponse<MovieSummaryResponse>(
            rows.Select(row => row.ToResponse()).ToList(), page, pageSize, totalCount);
    }

    /// <summary>Finds movies to log or watchlist: the user's manually added movies first, then TMDB's results.</summary>
    [HttpGet("search", Name = "searchMovies")]
    [ProducesResponseType<MovieSearchResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, MediaTypeNames.Application.ProblemJson)]
    public async Task<ActionResult<MovieSearchResponse>> SearchMovies(
        [FromQuery, Required, StringLength(200, MinimumLength = 1)] string query,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var items = await db.Movies
            .AsNoTracking()
            .Where(movie => movie.OwnerId == userId)
            .Search(query)
            .OrderBy(movie => movie.Title)
            .ThenBy(movie => movie.Id)
            .Take(MaxManualSearchResults)
            .Select(movie => new MovieSearchResult(
                movie.Id, null, movie.Title, movie.OriginalTitle, movie.ReleaseDate, movie.PosterUrl))
            .ToListAsync(cancellationToken);

        var tmdbResults = await SearchTmdbAsync(query.Trim(), cancellationToken);
        if (tmdbResults is null)
        {
            return new MovieSearchResponse(items, TmdbAvailable: false);
        }

        // Saves the client the import request for movies that are already known.
        var tmdbIds = tmdbResults.Select(result => (int?)result.TmdbId).ToList();
        var knownMovieIds = await db.Movies
            .Where(movie => tmdbIds.Contains(movie.TmdbId))
            .ToDictionaryAsync(movie => movie.TmdbId!.Value, movie => movie.Id, cancellationToken);

        items.AddRange(tmdbResults.Select(result => new MovieSearchResult(
            knownMovieIds.TryGetValue(result.TmdbId, out var movieId) ? movieId : null,
            result.TmdbId,
            result.Title,
            result.OriginalTitle,
            result.ReleaseDate,
            result.PosterUrl)));

        return new MovieSearchResponse(items, TmdbAvailable: true);
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
        var existingId = await FindIdByTmdbIdAsync(tmdbId, cancellationToken);
        if (existingId is not null)
        {
            return (await FindMovieResponseAsync(existingId.Value, cancellationToken))!;
        }

        if (!tmdb.IsConfigured)
        {
            return TmdbNotConfigured();
        }

        TmdbMovie? tmdbMovie;
        try
        {
            tmdbMovie = await tmdb.GetMovieAsync(tmdbId, cancellationToken);
        }
        catch (TmdbUnavailableException exception)
        {
            logger.LogWarning(exception, "Importing TMDB movie {TmdbId} failed", tmdbId);
            return Problem(
                statusCode: StatusCodes.Status502BadGateway,
                title: "TMDB is unavailable",
                detail: "The movie could not be loaded from TMDB. Please try again later.");
        }

        if (tmdbMovie is null)
        {
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Unknown TMDB movie",
                detail: $"TMDB has no movie with ID {tmdbId}.");
        }

        // TMDB genres that are not seeded (should not happen) are skipped.
        var genres = await db.Genres.Where(genre => tmdbMovie.GenreIds.Contains(genre.Id)).ToListAsync(cancellationToken);
        var movie = Movie.FromTmdb(tmdbId, tmdbMovie.Details, genres, timeProvider.GetUtcNow());
        db.Movies.Add(movie);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Someone else imported the same movie at the same moment.
            db.ChangeTracker.Clear();
            var concurrentId = await FindIdByTmdbIdAsync(tmdbId, cancellationToken);
            return (await FindMovieResponseAsync(concurrentId!.Value, cancellationToken))!;
        }

        var response = await FindMovieResponseAsync(movie.Id, cancellationToken);
        return CreatedAtRoute("getMovie", new { movieId = movie.Id }, response);
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

    /// <summary>Adds a movie manually, e.g. one that TMDB does not know. Only the signed-in user sees it.</summary>
    [HttpPost(Name = "createMovie")]
    [ProducesResponseType<MovieResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, MediaTypeNames.Application.ProblemJson)]
    public async Task<ActionResult<MovieResponse>> CreateMovie(
        MovieRequest request, CancellationToken cancellationToken)
    {
        var genres = await LoadGenresAsync(request.GenreIds!, cancellationToken);
        if (genres is null)
        {
            return ValidationProblem(ModelState);
        }

        var movie = Movie.CreateManual(User.GetUserId(), request.ToDetails(), genres, timeProvider.GetUtcNow());
        db.Movies.Add(movie);
        await db.SaveChangesAsync(cancellationToken);

        var response = await FindMovieResponseAsync(movie.Id, cancellationToken);
        return CreatedAtRoute("getMovie", new { movieId = movie.Id }, response);
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
        var movie = await VisibleMovies
            .Include(m => m.Genres)
            .SingleOrDefaultAsync(m => m.Id == movieId, cancellationToken);
        if (movie is null)
        {
            return NotFound();
        }

        if (!movie.IsManual)
        {
            return TmdbMovieIsReadOnly();
        }

        var genres = await LoadGenresAsync(request.GenreIds!, cancellationToken);
        if (genres is null)
        {
            return ValidationProblem(ModelState);
        }

        movie.Update(request.ToDetails(), genres);
        await db.SaveChangesAsync(cancellationToken);

        return (await FindMovieResponseAsync(movieId, cancellationToken))!;
    }

    /// <summary>Only manually added movies can be deleted, and only while they are not in the diary.</summary>
    [HttpDelete("{movieId:guid}", Name = "deleteMovie")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, MediaTypeNames.Application.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, MediaTypeNames.Application.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, MediaTypeNames.Application.ProblemJson)]
    public async Task<IActionResult> DeleteMovie(Guid movieId, CancellationToken cancellationToken)
    {
        var movie = await VisibleMovies.AsNoTracking().SingleOrDefaultAsync(m => m.Id == movieId, cancellationToken);
        if (movie is null)
        {
            return NotFound();
        }

        if (!movie.IsManual)
        {
            return TmdbMovieIsReadOnly();
        }

        if (await db.DiaryEntries.AnyAsync(entry => entry.MovieId == movieId, cancellationToken))
        {
            return Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Movie has diary entries",
                detail: "Movies that are in your diary cannot be deleted.");
        }

        // Watchlist items and genre assignments are removed by ON DELETE CASCADE in the database.
        await db.Movies.Where(m => m.Id == movieId).ExecuteDeleteAsync(cancellationToken);
        return NoContent();
    }

    [HttpGet("{movieId:guid}/diary-entries", Name = "listMovieDiaryEntries")]
    [ProducesResponseType<List<DiaryEntryResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, MediaTypeNames.Application.ProblemJson)]
    public async Task<ActionResult<List<DiaryEntryResponse>>> ListMovieDiaryEntries(
        Guid movieId, CancellationToken cancellationToken)
    {
        if (!await VisibleMovies.AnyAsync(movie => movie.Id == movieId, cancellationToken))
        {
            return NotFound();
        }

        var userId = User.GetUserId();
        var rows = await db.DiaryEntries
            .AsNoTracking()
            .Where(entry => entry.MovieId == movieId && entry.UserId == userId)
            .OrderByNewest()
            .SelectRows()
            .ToListAsync(cancellationToken);

        return rows.Select(row => row.ToResponse()).ToList();
    }

    /// <summary>Null if TMDB is not configured or failed; the search then only shows manual movies.</summary>
    private async Task<IReadOnlyList<TmdbSearchResult>?> SearchTmdbAsync(string query, CancellationToken cancellationToken)
    {
        if (!tmdb.IsConfigured)
        {
            return null;
        }

        try
        {
            return await tmdb.SearchMoviesAsync(query, cancellationToken);
        }
        catch (TmdbUnavailableException exception)
        {
            logger.LogWarning(exception, "TMDB search failed");
            return null;
        }
    }

    private async Task<Guid?> FindIdByTmdbIdAsync(int tmdbId, CancellationToken cancellationToken) =>
        await db.Movies
            .Where(movie => movie.TmdbId == tmdbId)
            .Select(movie => (Guid?)movie.Id)
            .SingleOrDefaultAsync(cancellationToken);

    private async Task<MovieResponse?> FindMovieResponseAsync(Guid movieId, CancellationToken cancellationToken)
    {
        var row = await VisibleMovies
            .AsNoTracking()
            .Where(movie => movie.Id == movieId)
            .SelectDetailRows(db, User.GetUserId())
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

    private ObjectResult TmdbMovieIsReadOnly() => Problem(
        statusCode: StatusCodes.Status403Forbidden,
        title: "Movie is read-only",
        detail: "Movies from TMDB cannot be edited or deleted. Only manually added movies can.");

    private ObjectResult TmdbNotConfigured() => Problem(
        statusCode: StatusCodes.Status503ServiceUnavailable,
        title: "TMDB is not configured",
        detail: "This server has no TMDB access token, so movies can only be added manually.");
}
