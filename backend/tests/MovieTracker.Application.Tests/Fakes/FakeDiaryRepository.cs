using MovieTracker.Application;
using MovieTracker.Application.Diary;
using MovieTracker.Application.Movies;
using MovieTracker.Domain.Diary;

namespace MovieTracker.Application.Tests.Fakes;

/// <summary>An in-memory diary that follows the contract of <see cref="IDiaryRepository"/>: users only see their own entries.</summary>
public sealed class FakeDiaryRepository(FakeMovieRepository movies) : IDiaryRepository
{
    public List<DiaryEntry> Entries { get; } = [];

    public (Guid UserId, DiaryFilter Filter, int Page, int PageSize)? LastList { get; private set; }

    public Task<PagedResult<DiaryEntryView>> ListAsync(
        Guid userId, DiaryFilter filter, int page, int pageSize, CancellationToken cancellationToken)
    {
        LastList = (userId, filter, page, pageSize);

        var matching = Newest(userId)
            .Where(entry => filter.From is null || entry.WatchedOn >= filter.From)
            .Where(entry => filter.To is null || entry.WatchedOn <= filter.To)
            .Where(entry => filter.MovieId is null || entry.MovieId == filter.MovieId)
            .ToList();
        var items = matching.Skip((page - 1) * pageSize).Take(pageSize).Select(ToView).ToList();
        return Task.FromResult(new PagedResult<DiaryEntryView>(items, page, pageSize, matching.Count));
    }

    public Task<IReadOnlyList<DiaryEntryView>> ListForMovieAsync(Guid userId, Guid movieId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<DiaryEntryView>>(
            Newest(userId).Where(entry => entry.MovieId == movieId).Select(ToView).ToList());

    public Task<DiaryEntryView?> GetAsync(Guid userId, Guid entryId, CancellationToken cancellationToken) =>
        Task.FromResult(Own(userId, entryId) is { } entry ? ToView(entry) : null);

    public Task<DiaryEntry?> FindAsync(Guid userId, Guid entryId, CancellationToken cancellationToken) =>
        Task.FromResult(Own(userId, entryId));

    public void Add(DiaryEntry entry) => Entries.Add(entry);

    public void Remove(DiaryEntry entry) => Entries.Remove(entry);

    private DiaryEntry? Own(Guid userId, Guid entryId) =>
        Entries.FirstOrDefault(entry => entry.UserId == userId && entry.Id == entryId);

    private IEnumerable<DiaryEntry> Newest(Guid userId) =>
        Entries
            .Where(entry => entry.UserId == userId)
            .OrderByDescending(entry => entry.WatchedOn)
            .ThenByDescending(entry => entry.CreatedAt)
            .ThenByDescending(entry => entry.Id);

    private DiaryEntryView ToView(DiaryEntry entry)
    {
        var movie = movies.Movies.Single(candidate => candidate.Id == entry.MovieId);
        return new DiaryEntryView(
            entry.Id,
            new MovieRef(movie.Id, movie.Title, movie.ReleaseDate, movie.PosterUrl),
            entry.WatchedOn,
            entry.Rating,
            entry.Review,
            entry.IsRewatch,
            entry.Liked,
            entry.CreatedAt);
    }
}
