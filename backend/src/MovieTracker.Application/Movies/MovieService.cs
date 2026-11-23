using Microsoft.Extensions.Logging;
using MovieTracker.Application.Activity;
using MovieTracker.Application.Genres;
using MovieTracker.Application.Tmdb;
using MovieTracker.Domain.Activity;
using MovieTracker.Domain.Movies;

namespace MovieTracker.Application.Movies;

/// <summary>
/// Movies come from TMDB (shared, read-only) or are added manually (private to their owner, editable).
/// Other users' manual movies are answered as if they did not exist.
/// </summary>
public sealed class MovieService(
    IMovieRepository movies,
    IGenreRepository genres,
    IActivityLogRepository activityLog,
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
        if (savedId != movie.Id)
        {
            return new ImportTmdbMovieResult(ImportTmdbMovieStatus.AlreadyImported, detail);
        }

        // The movie is saved already (shared, immediately), the log entry follows in its own save.
        activityLog.Add(ActivityLogEntry.ForMovie(
            userId, ActivityType.MovieImported, timeProvider.GetUtcNow(), movie.Id, movie.Title));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new ImportTmdbMovieResult(ImportTmdbMovieStatus.Imported, detail);
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

        // A new movie has no uploaded poster yet, so only an external URL is possible.
        if (details.PosterUrl is not null && !MoviePosterUrl.IsExternal(details.PosterUrl))
        {
            return new SaveMovieResult(SaveMovieStatus.InvalidPosterUrl);
        }

        var now = timeProvider.GetUtcNow();
        var movie = Movie.CreateManual(userId, details, movieGenres.Genres, now);
        movies.Add(movie);
        activityLog.Add(ActivityLogEntry.ForMovie(userId, ActivityType.MovieAdded, now, movie.Id, movie.Title));
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new SaveMovieResult(SaveMovieStatus.Saved, await movies.GetDetailAsync(userId, movie.Id, cancellationToken));
    }

    /// <summary>Only manually added movies can be changed.</summary>
    public async Task<SaveMovieResult> UpdateAsync(
        Guid userId, Guid movieId, MovieDetails details, IReadOnlyList<int> genreIds, CancellationToken cancellationToken)
    {
        var (movie, refusal) = await FindManualMovieAsync(userId, movieId, cancellationToken);
        if (movie is null)
        {
            return refusal!;
        }

        var movieGenres = await LoadGenresAsync(genreIds, cancellationToken);
        if (movieGenres.UnknownIds.Count > 0)
        {
            return new SaveMovieResult(SaveMovieStatus.UnknownGenres, UnknownGenreIds: movieGenres.UnknownIds);
        }

        // The movie's poster is its uploaded image as long as it keeps that image's URL. Any other URL (or none)
        // replaces the image, which is then deleted.
        var uploadedPosterUrl = MoviePosterUrl.For(movie.Id);
        var hasUploadedPoster = movie.PosterUrl == uploadedPosterUrl;
        var keepsUploadedPoster = hasUploadedPoster && details.PosterUrl == uploadedPosterUrl;
        if (details.PosterUrl is not null && !keepsUploadedPoster && !MoviePosterUrl.IsExternal(details.PosterUrl))
        {
            return new SaveMovieResult(SaveMovieStatus.InvalidPosterUrl);
        }

        if (hasUploadedPoster && !keepsUploadedPoster)
        {
            await RemovePosterImageAsync(movie.Id, cancellationToken);
        }

        movie.Update(details, movieGenres.Genres);
        activityLog.Add(ActivityLogEntry.ForMovie(
            userId, ActivityType.MovieUpdated, timeProvider.GetUtcNow(), movie.Id, movie.Title));
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new SaveMovieResult(SaveMovieStatus.Saved, await movies.GetDetailAsync(userId, movieId, cancellationToken));
    }

    /// <summary>The image of a movie's uploaded poster. Null if the movie does not exist, is not visible to the user or has none.</summary>
    public Task<PosterImage?> GetPosterAsync(Guid userId, Guid movieId, CancellationToken cancellationToken) =>
        movies.GetPosterAsync(userId, movieId, cancellationToken);

    /// <summary>
    /// Uploads the poster of a manually added movie, replacing its previous poster (an uploaded image or a URL).
    /// </summary>
    public async Task<SaveMovieResult> SetPosterAsync(
        Guid userId, Guid movieId, byte[] image, CancellationToken cancellationToken)
    {
        var (movie, refusal) = await FindManualMovieAsync(userId, movieId, cancellationToken);
        if (movie is null)
        {
            return refusal!;
        }

        var now = timeProvider.GetUtcNow();
        var poster = await movies.FindPosterAsync(movie.Id, cancellationToken);
        if (poster is null)
        {
            poster = MoviePoster.TryCreate(movie.Id, image, now);
            if (poster is null)
            {
                return new SaveMovieResult(SaveMovieStatus.InvalidPosterImage);
            }

            movies.AddPoster(poster);
        }
        else if (!poster.TryReplace(image, now))
        {
            return new SaveMovieResult(SaveMovieStatus.InvalidPosterImage);
        }

        movie.SetPosterUrl(MoviePosterUrl.For(movie.Id));
        activityLog.Add(ActivityLogEntry.ForMovie(userId, ActivityType.MovieUpdated, now, movie.Id, movie.Title));
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

        movies.Remove(movie);
        // No movie ID: the movie is gone, the log only keeps its title.
        activityLog.Add(ActivityLogEntry.ForMovie(
            userId, ActivityType.MovieDeleted, timeProvider.GetUtcNow(), movieId: null, movie.Title));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return DeleteMovieResult.Deleted;
    }

    /// <summary>
    /// The movie to change, or why it cannot be changed: it does not exist or is not visible to the user (NotFound),
    /// or it comes from TMDB (ReadOnly).
    /// </summary>
    private async Task<(Movie? Movie, SaveMovieResult? Refusal)> FindManualMovieAsync(
        Guid userId, Guid movieId, CancellationToken cancellationToken)
    {
        var movie = await movies.FindAsync(userId, movieId, cancellationToken);
        if (movie is null)
        {
            return (null, new SaveMovieResult(SaveMovieStatus.NotFound));
        }

        return movie.IsManual ? (movie, null) : (null, new SaveMovieResult(SaveMovieStatus.ReadOnly));
    }

    private async Task RemovePosterImageAsync(Guid movieId, CancellationToken cancellationToken)
    {
        var poster = await movies.FindPosterAsync(movieId, cancellationToken);
        if (poster is not null)
        {
            movies.RemovePoster(poster);
        }
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
