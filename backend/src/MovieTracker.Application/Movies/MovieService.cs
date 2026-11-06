using Microsoft.Extensions.Logging;
using MovieTracker.Application.Genres;
using MovieTracker.Application.Tmdb;
using MovieTracker.Domain.Movies;

namespace MovieTracker.Application.Movies;

/// <summary>
/// Movies come from TMDB (shared, read-only) or are added manually (private to their owner, editable).
/// Other users' manual movies are answered as if they did not exist.
/// </summary>
public sealed class MovieService(
    IMovieRepository movies,
    IGenreRepository genres,
    IUnitOfWork unitOfWork,
    ITmdbClient tmdb,
    TimeProvider timeProvider,
    ILogger<MovieService> logger)
{
    private const int MaxManualSearchResults = 10;

    public Task<PagedResult<MovieSummary>> ListAsync(Guid userId, MovieListQuery query, CancellationToken cancellationToken) =>
        movies.ListInLibraryAsync(userId, query, cancellationToken);

    /// <summary>Finds movies to log or watchlist: the user's manually added movies first, then TMDB's results.</summary>
    public async Task<MovieSearchOutcome> SearchAsync(Guid userId, string query, CancellationToken cancellationToken)
    {
        var hits = (await movies.SearchOwnAsync(userId, query, MaxManualSearchResults, cancellationToken)).ToList();

        var tmdbResults = await SearchTmdbAsync(query.Trim(), cancellationToken);
        if (tmdbResults is null)
        {
            return new MovieSearchOutcome(hits, TmdbAvailable: false);
        }

        // Saves the client the import request for movies that are already known.
        var knownMovieIds = await movies.FindIdsByTmdbIdsAsync(
            tmdbResults.Select(result => result.TmdbId).ToList(), cancellationToken);

        hits.AddRange(tmdbResults.Select(result => new MovieSearchHit(
            knownMovieIds.TryGetValue(result.TmdbId, out var movieId) ? movieId : null,
            result.TmdbId,
            result.Title,
            result.OriginalTitle,
            result.ReleaseDate,
            result.PosterUrl)));

        return new MovieSearchOutcome(hits, TmdbAvailable: true);
    }

    /// <summary>Makes a TMDB movie available for diary and watchlist. Importing it again is harmless.</summary>
    public async Task<ImportTmdbMovieResult> ImportTmdbMovieAsync(Guid userId, int tmdbId, CancellationToken cancellationToken)
    {
        var existingId = await movies.FindIdByTmdbIdAsync(tmdbId, cancellationToken);
        if (existingId is not null)
        {
            var existing = await movies.GetDetailAsync(userId, existingId.Value, cancellationToken);
            return new ImportTmdbMovieResult(ImportTmdbMovieStatus.AlreadyImported, existing);
        }

        if (!tmdb.IsConfigured)
        {
            return new ImportTmdbMovieResult(ImportTmdbMovieStatus.TmdbNotConfigured);
        }

        TmdbMovie? tmdbMovie;
        try
        {
            tmdbMovie = await tmdb.GetMovieAsync(tmdbId, cancellationToken);
        }
        catch (TmdbUnavailableException exception)
        {
            logger.LogWarning(exception, "Importing TMDB movie {TmdbId} failed", tmdbId);
            return new ImportTmdbMovieResult(ImportTmdbMovieStatus.TmdbUnavailable);
        }

        if (tmdbMovie is null)
        {
            return new ImportTmdbMovieResult(ImportTmdbMovieStatus.UnknownTmdbMovie);
        }

        // TMDB genres that are not seeded (should not happen) are skipped.
        var movieGenres = await genres.FindByIdsAsync(tmdbMovie.GenreIds, cancellationToken);
        var movie = Movie.FromTmdb(tmdbId, tmdbMovie.Details, movieGenres, timeProvider.GetUtcNow());
        var savedId = await movies.AddTmdbMovieAsync(movie, cancellationToken);

        var detail = await movies.GetDetailAsync(userId, savedId, cancellationToken);
        // Not the same ID: someone else imported the movie at the same moment.
        var status = savedId == movie.Id ? ImportTmdbMovieStatus.Imported : ImportTmdbMovieStatus.AlreadyImported;
        return new ImportTmdbMovieResult(status, detail);
    }

    /// <summary>Null if the movie does not exist or is not visible to the user.</summary>
    public Task<MovieDetail?> GetAsync(Guid userId, Guid movieId, CancellationToken cancellationToken) =>
        movies.GetDetailAsync(userId, movieId, cancellationToken);

    /// <summary>Adds a movie manually, e.g. one that TMDB does not know. Only the user sees it.</summary>
    public async Task<SaveMovieResult> CreateAsync(
        Guid userId, MovieDetails details, IReadOnlyList<int> genreIds, CancellationToken cancellationToken)
    {
        var movieGenres = await LoadGenresAsync(genreIds, cancellationToken);
        if (movieGenres.UnknownIds.Count > 0)
        {
            return new SaveMovieResult(SaveMovieStatus.UnknownGenres, UnknownGenreIds: movieGenres.UnknownIds);
        }

        var movie = Movie.CreateManual(userId, details, movieGenres.Genres, timeProvider.GetUtcNow());
        movies.Add(movie);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new SaveMovieResult(SaveMovieStatus.Saved, await movies.GetDetailAsync(userId, movie.Id, cancellationToken));
    }

    /// <summary>Only manually added movies can be changed.</summary>
    public async Task<SaveMovieResult> UpdateAsync(
        Guid userId, Guid movieId, MovieDetails details, IReadOnlyList<int> genreIds, CancellationToken cancellationToken)
    {
        var movie = await movies.FindAsync(userId, movieId, cancellationToken);
        if (movie is null)
        {
            return new SaveMovieResult(SaveMovieStatus.NotFound);
        }

        if (!movie.IsManual)
        {
            return new SaveMovieResult(SaveMovieStatus.ReadOnly);
        }

        var movieGenres = await LoadGenresAsync(genreIds, cancellationToken);
        if (movieGenres.UnknownIds.Count > 0)
        {
            return new SaveMovieResult(SaveMovieStatus.UnknownGenres, UnknownGenreIds: movieGenres.UnknownIds);
        }

        movie.Update(details, movieGenres.Genres);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new SaveMovieResult(SaveMovieStatus.Saved, await movies.GetDetailAsync(userId, movieId, cancellationToken));
    }

    /// <summary>Only manually added movies can be deleted, and only while they are not in the diary.</summary>
    public async Task<DeleteMovieResult> DeleteAsync(Guid userId, Guid movieId, CancellationToken cancellationToken)
    {
        var movie = await movies.FindAsync(userId, movieId, cancellationToken);
        if (movie is null)
        {
            return DeleteMovieResult.NotFound;
        }

        if (!movie.IsManual)
        {
            return DeleteMovieResult.ReadOnly;
        }

        if (await movies.HasDiaryEntriesAsync(movieId, cancellationToken))
        {
            return DeleteMovieResult.HasDiaryEntries;
        }

        await movies.DeleteAsync(movieId, cancellationToken);
        return DeleteMovieResult.Deleted;
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

    private async Task<(IReadOnlyList<Genre> Genres, IReadOnlyList<int> UnknownIds)> LoadGenresAsync(
        IReadOnlyList<int> genreIds, CancellationToken cancellationToken)
    {
        var requestedIds = genreIds.Distinct().ToList();
        var found = await genres.FindByIdsAsync(requestedIds, cancellationToken);
        var unknownIds = requestedIds.Except(found.Select(genre => genre.Id)).ToList();
        return (found, unknownIds);
    }
}
