using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MovieTracker.Domain.Diary;
using MovieTracker.Domain.Movies;
using MovieTracker.Domain.Watchlist;
using MovieTracker.Infrastructure.Persistence;
using MovieTracker.Application.Tmdb;
using Npgsql;

namespace MovieTracker.Infrastructure.Letterboxd;

/// <summary>
/// Imports a Letterboxd export for one user. The export only names the films, so each one is first mapped to a movie:
/// <list type="number">
/// <item>A movie the user can already use (shared TMDB movie or own manual movie) with the same title and year.</item>
/// <item>Otherwise the match on TMDB, which is saved like any movie imported from TMDB.</item>
/// <item>Otherwise a manual movie with the name and year from Letterboxd.</item>
/// </list>
/// If TMDB fails for a film, the film is left out instead of creating a manual movie that would never be replaced by the
/// real one. Importing the same export again is safe: viewings and watchlist items that exist are skipped.
/// </summary>
public sealed class LetterboxdImporter(
    MovieTrackerDbContext db,
    ITmdbClient tmdb,
    TimeProvider timeProvider,
    ILogger<LetterboxdImporter> logger)
{
    /// <summary>
    /// A few films at once make a big import fast. Every film takes one to three requests, so this stays below
    /// TMDB's limit of about 50 requests per second.
    /// </summary>
    private const int TmdbParallelism = 3;

    // Same limits as the database columns.
    private const int MaxTitleLength = 300;
    private const int MaxReviewLength = 20000;

    private const int MaxSaveAttempts = 3;

    /// <param name="progress">Filled while the import runs, so the caller can show how far it is.</param>
    public async Task<LetterboxdImportResult> ImportAsync(
        Guid userId, LetterboxdExport export, LetterboxdImportProgress progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(export);
        ArgumentNullException.ThrowIfNull(progress);

        var films = export.Diary.Select(entry => entry.Film)
            .Concat(export.Watchlist.Select(item => item.Film))
            .Distinct()
            .ToList();
        progress.Start(films.Count);

        var movies = await ResolveMoviesAsync(userId, progress, films, cancellationToken);

        var now = timeProvider.GetUtcNow();
        var diary = await AddDiaryEntriesAsync(userId, export.Diary, movies.MovieIds, now, cancellationToken);
        var watchlist = await AddWatchlistItemsAsync(userId, export.Watchlist, movies.MovieIds, cancellationToken);
        // One SaveChanges is one transaction: either the whole diary and watchlist is imported or nothing.
        await db.SaveChangesAsync(cancellationToken);

        return new LetterboxdImportResult(
            diary.Imported,
            diary.Skipped,
            watchlist.Imported,
            watchlist.Skipped,
            movies.ManualFilms,
            movies.FailedFilms,
            export.Warnings);
    }

    private async Task<ResolvedMovies> ResolveMoviesAsync(
        Guid userId, LetterboxdImportProgress progress, IReadOnlyList<LetterboxdFilm> films, CancellationToken cancellationToken)
    {
        var movieIds = new Dictionary<LetterboxdFilm, Guid>();

        // 1. Movies that exist already, which saves the TMDB requests (and makes a repeated import fast).
        var unknownFilms = new List<LetterboxdFilm>();
        var knownMovies = await FindKnownMoviesAsync(userId, films, cancellationToken);
        foreach (var film in films)
        {
            if (FindKnownMovie(film, knownMovies) is { } movieId)
            {
                movieIds[film] = movieId;
                progress.FilmProcessed();
            }
            else
            {
                unknownFilms.Add(film);
            }
        }

        // 2. TMDB, a few films at a time. Only the network is used here, the database context is not thread-safe.
        var lookups = new ConcurrentDictionary<LetterboxdFilm, TmdbLookup>();
        if (tmdb.IsConfigured && unknownFilms.Count > 0)
        {
            var tmdbMovieIds = await FindMovieIdsByTmdbIdAsync(tmdbIds: null, cancellationToken);

            await Parallel.ForEachAsync(
                unknownFilms,
                new ParallelOptions { MaxDegreeOfParallelism = TmdbParallelism, CancellationToken = cancellationToken },
                async (film, token) =>
                {
                    lookups[film] = await LookUpOnTmdbAsync(film, tmdbMovieIds, token);
                    progress.FilmProcessed();
                });
        }
        else
        {
            foreach (var _ in unknownFilms)
            {
                progress.FilmProcessed();
            }
        }

        // 3. Save what TMDB found, and manual movies for the rest.
        var failedFilms = new List<LetterboxdFilm>();
        var manualFilms = new List<LetterboxdFilm>();
        var tmdbMoviesToSave = new Dictionary<int, TmdbMovie>();
        var filmsByTmdbId = new List<(LetterboxdFilm Film, int TmdbId)>();

        foreach (var film in unknownFilms)
        {
            // Without a lookup, TMDB is not configured.
            if (!lookups.TryGetValue(film, out var lookup) || lookup.IsNotFound)
            {
                manualFilms.Add(film);
            }
            else if (lookup.IsFailed)
            {
                failedFilms.Add(film);
            }
            else if (lookup.ExistingMovieId is { } existingMovieId)
            {
                movieIds[film] = existingMovieId;
            }
            else
            {
                tmdbMoviesToSave[lookup.NewMovie!.TmdbId] = lookup.NewMovie;
                filmsByTmdbId.Add((film, lookup.NewMovie.TmdbId));
            }
        }

        var saved = await SaveMoviesAsync(userId, tmdbMoviesToSave.Values.ToList(), manualFilms, cancellationToken);
        foreach (var (film, tmdbId) in filmsByTmdbId)
        {
            movieIds[film] = saved.TmdbMovieIds[tmdbId];
        }

        foreach (var (film, movieId) in saved.ManualMovieIds)
        {
            movieIds[film] = movieId;
        }

        return new ResolvedMovies(movieIds, manualFilms, failedFilms);
    }

    private async Task<List<KnownMovie>> FindKnownMoviesAsync(
        Guid userId, IReadOnlyList<LetterboxdFilm> films, CancellationToken cancellationToken)
    {
        var names = films.Select(film => film.Name).Distinct().ToList();

        // Exact names here; FilmMatcher then compares loosely (case, accents, punctuation) among the candidates.
        return await db.Movies
            .AsNoTracking()
            // Shared TMDB movies and the user's own manual movies (the same rule as Movie.IsVisibleTo).
            .Where(movie => movie.OwnerId == null || movie.OwnerId == userId)
            .Where(movie => names.Contains(movie.Title) || (movie.OriginalTitle != null && names.Contains(movie.OriginalTitle)))
            .Select(movie => new KnownMovie(movie.Id, movie.Title, movie.OriginalTitle, movie.ReleaseDate, movie.OwnerId != null))
            .ToListAsync(cancellationToken);
    }

    private static Guid? FindKnownMovie(LetterboxdFilm film, List<KnownMovie> knownMovies)
    {
        Guid? best = null;
        var bestScore = int.MaxValue;

        foreach (var movie in knownMovies)
        {
            if (FilmMatcher.Match(film, movie.Title, movie.OriginalTitle, movie.ReleaseDate) is not { } distance)
            {
                continue;
            }

            // The better year wins; a TMDB movie beats a manual one of the same quality.
            var score = (distance * 2) + (movie.IsManual ? 1 : 0);
            if (score < bestScore)
            {
                best = movie.Id;
                bestScore = score;
            }
        }

        return best;
    }

    /// <summary>Never throws because of TMDB: a film that cannot be looked up is marked as failed.</summary>
    private async Task<TmdbLookup> LookUpOnTmdbAsync(
        LetterboxdFilm film, IReadOnlyDictionary<int, Guid> tmdbMovieIds, CancellationToken cancellationToken)
    {
        try
        {
            var match = await FindOnTmdbAsync(film, cancellationToken);
            if (match is null)
            {
                return TmdbLookup.NotFound;
            }

            if (tmdbMovieIds.TryGetValue(match.TmdbId, out var existingMovieId))
            {
                return TmdbLookup.Existing(existingMovieId);
            }

            var movie = await tmdb.GetMovieAsync(match.TmdbId, cancellationToken);
            return movie is null ? TmdbLookup.NotFound : TmdbLookup.New(movie);
        }
        catch (TmdbUnavailableException exception)
        {
            logger.LogWarning(exception, "Looking up a Letterboxd film on TMDB failed");
            return TmdbLookup.Failed;
        }
    }

    private async Task<TmdbSearchResult?> FindOnTmdbAsync(LetterboxdFilm film, CancellationToken cancellationToken)
    {
        if (film.Year is not null)
        {
            var inYear = FilmMatcher.FindBest(film, await tmdb.FindMoviesAsync(film.Name, film.Year, cancellationToken));
            if (inYear is not null)
            {
                return inYear;
            }
        }

        // The year filter can miss a film whose release year differs by one, or be too narrow for TMDB's data.
        return FilmMatcher.FindBest(film, await tmdb.FindMoviesAsync(film.Name, year: null, cancellationToken));
    }

    /// <summary>The IDs of the saved TMDB movies by TMDB ID, of all of them if <paramref name="tmdbIds"/> is null.</summary>
    private async Task<Dictionary<int, Guid>> FindMovieIdsByTmdbIdAsync(
        IReadOnlyCollection<int>? tmdbIds, CancellationToken cancellationToken)
    {
        var movies = db.Movies.AsNoTracking().Where(movie => movie.TmdbId != null);
        if (tmdbIds is not null)
        {
            movies = movies.Where(movie => tmdbIds.Contains(movie.TmdbId!.Value));
        }

        var rows = await movies.Select(movie => new { TmdbId = movie.TmdbId!.Value, movie.Id }).ToListAsync(cancellationToken);
        return rows.ToDictionary(row => row.TmdbId, row => row.Id);
    }

    /// <summary>Saves the movies in one transaction and returns the IDs of the movies, existing or new.</summary>
    private async Task<SavedMovies> SaveMoviesAsync(
        Guid userId,
        IReadOnlyList<TmdbMovie> tmdbMovies,
        IReadOnlyList<LetterboxdFilm> manualFilms,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            var now = timeProvider.GetUtcNow();
            var genres = await db.Genres.ToListAsync(cancellationToken);

            // Another import may have saved some of the movies since they were looked up.
            var tmdbMovieIds = await FindMovieIdsByTmdbIdAsync(tmdbMovies.Select(movie => movie.TmdbId).ToList(), cancellationToken);

            foreach (var tmdbMovie in tmdbMovies.Where(movie => !tmdbMovieIds.ContainsKey(movie.TmdbId)))
            {
                // Genres that are not seeded are skipped, like when importing a single movie.
                var movieGenres = genres.Where(genre => tmdbMovie.GenreIds.Contains(genre.Id));
                var movie = Movie.FromTmdb(tmdbMovie.TmdbId, tmdbMovie.Details, movieGenres, now);
                db.Movies.Add(movie);
                tmdbMovieIds[tmdbMovie.TmdbId] = movie.Id;
            }

            var manualMovieIds = new Dictionary<LetterboxdFilm, Guid>();
            foreach (var film in manualFilms)
            {
                var movie = Movie.CreateManual(userId, ToManualMovieDetails(film), genres: [], now);
                db.Movies.Add(movie);
                manualMovieIds[film] = movie.Id;
            }

            try
            {
                await db.SaveChangesAsync(cancellationToken);
                return new SavedMovies(tmdbMovieIds, manualMovieIds);
            }
            catch (DbUpdateException exception) when (
                attempt < MaxSaveAttempts
                && exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                // Someone else saved one of the TMDB movies at the same moment: start over, now it exists.
                db.ChangeTracker.Clear();
            }
        }
    }

    private static MovieDetails ToManualMovieDetails(LetterboxdFilm film)
    {
        var name = film.Name.Length > MaxTitleLength ? film.Name[..MaxTitleLength] : film.Name;
        // Letterboxd only tells the year, so the movie is dated to the first day of it.
        DateOnly? releaseDate = film.Year is { } year ? new DateOnly(year, 1, 1) : null;
        return new MovieDetails(name, OriginalTitle: null, releaseDate, RuntimeMinutes: null, Overview: null, PosterUrl: null);
    }

    private async Task<Counts> AddDiaryEntriesAsync(
        Guid userId,
        IReadOnlyList<LetterboxdDiaryEntry> entries,
        IReadOnlyDictionary<LetterboxdFilm, Guid> movieIds,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var importable = entries.Where(entry => movieIds.ContainsKey(entry.Film)).ToList();
        var importedMovieIds = importable.Select(entry => movieIds[entry.Film]).Distinct().ToList();

        // How often each movie is logged per day already. Logging a movie twice on one day is allowed, so
        // duplicates are counted off one by one instead of skipping every entry of a day that has one.
        var existingCounts = (await db.DiaryEntries
                .AsNoTracking()
                .Where(entry => entry.UserId == userId && importedMovieIds.Contains(entry.MovieId))
                .GroupBy(entry => new { entry.MovieId, entry.WatchedOn })
                .Select(group => new { group.Key.MovieId, group.Key.WatchedOn, Count = group.Count() })
                .ToListAsync(cancellationToken))
            .ToDictionary(row => (row.MovieId, row.WatchedOn), row => row.Count);

        var imported = 0;
        var skipped = 0;
        foreach (var entry in importable)
        {
            var movieId = movieIds[entry.Film];
            var key = (movieId, entry.WatchedOn);
            if (existingCounts.TryGetValue(key, out var existing) && existing > 0)
            {
                existingCounts[key] = existing - 1;
                skipped++;
                continue;
            }

            var review = entry.Review is { Length: > MaxReviewLength } ? entry.Review[..MaxReviewLength] : entry.Review;
            var details = new DiaryEntryDetails(
                movieId,
                entry.WatchedOn,
                entry.Rating is { } halfStars ? new Rating(halfStars) : null,
                review,
                entry.IsRewatch,
                entry.Liked);
            db.DiaryEntries.Add(new DiaryEntry(userId, details, now));
            imported++;
        }

        return new Counts(imported, skipped);
    }

    private async Task<Counts> AddWatchlistItemsAsync(
        Guid userId,
        IReadOnlyList<LetterboxdWatchlistItem> items,
        IReadOnlyDictionary<LetterboxdFilm, Guid> movieIds,
        CancellationToken cancellationToken)
    {
        var importable = items.Where(item => movieIds.ContainsKey(item.Film)).ToList();
        var importedMovieIds = importable.Select(item => movieIds[item.Film]).Distinct().ToList();

        var onWatchlist = (await db.WatchlistItems
                .AsNoTracking()
                .Where(item => item.UserId == userId && importedMovieIds.Contains(item.MovieId))
                .Select(item => item.MovieId)
                .ToListAsync(cancellationToken))
            .ToHashSet();

        var imported = 0;
        var skipped = 0;
        foreach (var item in importable)
        {
            var movieId = movieIds[item.Film];
            // Also true for a movie that appeared before in the same export.
            if (!onWatchlist.Add(movieId))
            {
                skipped++;
                continue;
            }

            // The date matters: the watchlist is ordered by it.
            var addedAt = new DateTimeOffset(item.AddedOn.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            db.WatchlistItems.Add(new WatchlistItem(userId, movieId, note: null, addedAt));
            imported++;
        }

        return new Counts(imported, skipped);
    }

    private sealed record KnownMovie(Guid Id, string Title, string? OriginalTitle, DateOnly? ReleaseDate, bool IsManual);

    private sealed record Counts(int Imported, int Skipped);

    private sealed record SavedMovies(
        IReadOnlyDictionary<int, Guid> TmdbMovieIds, IReadOnlyDictionary<LetterboxdFilm, Guid> ManualMovieIds);

    private sealed record ResolvedMovies(
        IReadOnlyDictionary<LetterboxdFilm, Guid> MovieIds,
        IReadOnlyList<LetterboxdFilm> ManualFilms,
        IReadOnlyList<LetterboxdFilm> FailedFilms);

    /// <summary>What TMDB knows about a film: nothing, a movie that is saved already, a movie to save, or an error.</summary>
    private sealed record TmdbLookup(Guid? ExistingMovieId, TmdbMovie? NewMovie, bool IsFailed)
    {
        public static readonly TmdbLookup NotFound = new(null, null, false);

        public static readonly TmdbLookup Failed = new(null, null, true);

        public bool IsNotFound => ExistingMovieId is null && NewMovie is null && !IsFailed;

        public static TmdbLookup Existing(Guid movieId) => new(movieId, null, false);

        public static TmdbLookup New(TmdbMovie movie) => new(null, movie, false);
    }
}
