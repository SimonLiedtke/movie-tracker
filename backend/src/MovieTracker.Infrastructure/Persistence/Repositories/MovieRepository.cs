using Microsoft.EntityFrameworkCore;
using MovieTracker.Application;
using MovieTracker.Application.Movies;
using MovieTracker.Domain.Movies;
using MovieTracker.Infrastructure.Persistence.Queries;
using Npgsql;

namespace MovieTracker.Infrastructure.Persistence.Repositories;

internal sealed class MovieRepository(MovieTrackerDbContext db) : IMovieRepository
{
    public async Task<PagedResult<MovieSummary>> ListInLibraryAsync(
        Guid userId, MovieListQuery query, CancellationToken cancellationToken)
    {
        var movies = db.Movies.AsNoTracking().InLibraryOf(db, userId).Search(query.Search);

        if (query.GenreId is { } genreId)
        {
            movies = movies.Where(movie => movie.Genres.Any(genre => genre.Id == genreId));
        }

        if (query.Year is { } year)
        {
            // Range comparison instead of ReleaseDate.Year, so an index on release_date could be used.
            var firstDay = new DateOnly(year, 1, 1);
            var firstDayOfNextYear = firstDay.AddYears(1);
            movies = movies.Where(movie => movie.ReleaseDate >= firstDay && movie.ReleaseDate < firstDayOfNextYear);
        }

        var ordered = query.Sort switch
        {
            MovieSort.TitleDescending => movies.OrderByDescending(movie => movie.Title),
            MovieSort.ReleaseDate => movies.OrderBy(movie => movie.ReleaseDate),
            MovieSort.ReleaseDateDescending => movies.OrderByDescending(movie => movie.ReleaseDate),
            MovieSort.CreatedAt => movies.OrderBy(movie => movie.CreatedAt),
            MovieSort.CreatedAtDescending => movies.OrderByDescending(movie => movie.CreatedAt),
            _ => movies.OrderBy(movie => movie.Title),
        };

        var totalCount = await movies.CountAsync(cancellationToken);
        var rows = await ordered
            .ThenBy(movie => movie.Id) // stable order for paging
            .Paginate(query.Page, query.PageSize)
            .SelectSummaryRows(db, userId)
            .ToListAsync(cancellationToken);

        return new PagedResult<MovieSummary>(
            rows.Select(row => row.ToModel()).ToList(), query.Page, query.PageSize, totalCount);
    }

    public async Task<IReadOnlyList<MovieSearchHit>> SearchOwnAsync(
        Guid userId, string query, int maxResults, CancellationToken cancellationToken) =>
        await db.Movies
            .AsNoTracking()
            .Where(movie => movie.OwnerId == userId)
            .Search(query)
            .OrderBy(movie => movie.Title)
            .ThenBy(movie => movie.Id)
            .Take(maxResults)
            .Select(movie => new MovieSearchHit(
                movie.Id, null, movie.Title, movie.OriginalTitle, movie.ReleaseDate, movie.PosterUrl))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<int, Guid>> FindIdsByTmdbIdsAsync(
        IReadOnlyCollection<int> tmdbIds, CancellationToken cancellationToken)
    {
        var ids = tmdbIds.Select(id => (int?)id).ToList();
        var rows = await db.Movies
            .AsNoTracking()
            .Where(movie => ids.Contains(movie.TmdbId))
            .Select(movie => new { TmdbId = movie.TmdbId!.Value, movie.Id })
            .ToListAsync(cancellationToken);
        return rows.ToDictionary(row => row.TmdbId, row => row.Id);
    }

    public async Task<Guid?> FindIdByTmdbIdAsync(int tmdbId, CancellationToken cancellationToken) =>
        await db.Movies
            .Where(movie => movie.TmdbId == tmdbId)
            .Select(movie => (Guid?)movie.Id)
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<MovieDetail?> GetDetailAsync(Guid userId, Guid movieId, CancellationToken cancellationToken)
    {
        var row = await db.Movies
            .AsNoTracking()
            .VisibleTo(userId)
            .Where(movie => movie.Id == movieId)
            .SelectDetailRows(db, userId)
            .SingleOrDefaultAsync(cancellationToken);

        return row?.ToModel();
    }

    public async Task<bool> ExistsAsync(Guid userId, Guid movieId, CancellationToken cancellationToken) =>
        await db.Movies.VisibleTo(userId).AnyAsync(movie => movie.Id == movieId, cancellationToken);

    public async Task<Movie?> FindAsync(Guid userId, Guid movieId, CancellationToken cancellationToken) =>
        await db.Movies
            .VisibleTo(userId)
            .Include(movie => movie.Genres)
            .SingleOrDefaultAsync(movie => movie.Id == movieId, cancellationToken);

    public void Add(Movie movie) => db.Movies.Add(movie);

    public async Task<Guid> AddTmdbMovieAsync(Movie movie, CancellationToken cancellationToken)
    {
        db.Movies.Add(movie);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return movie.Id;
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Someone else imported the same movie at the same moment.
            db.ChangeTracker.Clear();
            return (await FindIdByTmdbIdAsync(movie.TmdbId!.Value, cancellationToken))!.Value;
        }
    }

    public async Task<bool> HasDiaryEntriesAsync(Guid movieId, CancellationToken cancellationToken) =>
        await db.DiaryEntries.AnyAsync(entry => entry.MovieId == movieId, cancellationToken);

    public async Task DeleteAsync(Guid movieId, CancellationToken cancellationToken) =>
        // Watchlist items and genre assignments are removed by ON DELETE CASCADE in the database.
        await db.Movies.Where(movie => movie.Id == movieId).ExecuteDeleteAsync(cancellationToken);
}
