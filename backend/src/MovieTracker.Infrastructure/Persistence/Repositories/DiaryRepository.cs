using Microsoft.EntityFrameworkCore;
using MovieTracker.Application;
using MovieTracker.Application.Diary;
using MovieTracker.Domain.Diary;
using MovieTracker.Infrastructure.Persistence.Queries;

namespace MovieTracker.Infrastructure.Persistence.Repositories;

internal sealed class DiaryRepository(MovieTrackerDbContext db) : IDiaryRepository
{
    private IQueryable<DiaryEntry> OwnEntries(Guid userId) => db.DiaryEntries.Where(entry => entry.UserId == userId);

    public async Task<PagedResult<DiaryEntryView>> ListAsync(
        Guid userId, DiaryFilter filter, int page, int pageSize, CancellationToken cancellationToken)
    {
        var entries = OwnEntries(userId).AsNoTracking();

        if (filter.From is { } from)
        {
            entries = entries.Where(entry => entry.WatchedOn >= from);
        }

        if (filter.To is { } to)
        {
            entries = entries.Where(entry => entry.WatchedOn <= to);
        }

        if (filter.MovieId is { } movieId)
        {
            entries = entries.Where(entry => entry.MovieId == movieId);
        }

        if (filter.Tag is { } tag)
        {
            entries = entries.Where(entry => entry.Tags.Contains(tag));
        }

        if (filter.RatingFrom is not null || filter.RatingTo is not null)
        {
            // Rating is value-converted, so EF Core can only compare it for equality: list the ratings in the range.
            var lowest = filter.RatingFrom?.HalfStars ?? Rating.MinHalfStars;
            var highest = filter.RatingTo?.HalfStars ?? Rating.MaxHalfStars;
            var ratings = Enumerable.Range(lowest, Math.Max(0, highest - lowest + 1))
                .Select(halfStars => new Rating(halfStars))
                .ToArray();
            entries = entries.Where(entry => ratings.Contains(entry.Rating!.Value));
        }

        var totalCount = await entries.CountAsync(cancellationToken);
        var rows = await entries
            .OrderByNewest()
            .Paginate(page, pageSize)
            .SelectRows()
            .ToListAsync(cancellationToken);

        return new PagedResult<DiaryEntryView>(rows.Select(row => row.ToModel()).ToList(), page, pageSize, totalCount);
    }

    public async Task<IReadOnlyList<DiaryEntryView>> ListForMovieAsync(
        Guid userId, Guid movieId, CancellationToken cancellationToken)
    {
        var rows = await OwnEntries(userId)
            .AsNoTracking()
            .Where(entry => entry.MovieId == movieId)
            .OrderByNewest()
            .SelectRows()
            .ToListAsync(cancellationToken);

        return rows.Select(row => row.ToModel()).ToList();
    }

    public async Task<DiaryEntryView?> GetAsync(Guid userId, Guid entryId, CancellationToken cancellationToken)
    {
        var row = await OwnEntries(userId)
            .AsNoTracking()
            .Where(entry => entry.Id == entryId)
            .SelectRows()
            .SingleOrDefaultAsync(cancellationToken);

        return row?.ToModel();
    }

    public async Task<DiaryEntry?> FindAsync(Guid userId, Guid entryId, CancellationToken cancellationToken) =>
        await OwnEntries(userId).SingleOrDefaultAsync(entry => entry.Id == entryId, cancellationToken);

    public void Add(DiaryEntry entry) => db.DiaryEntries.Add(entry);

    public void Remove(DiaryEntry entry) => db.DiaryEntries.Remove(entry);
}
