using MovieTracker.Domain.Diary;

namespace MovieTracker.Application.Diary;

/// <summary>Every method only sees the entries of the given user: other users' entries do not exist for them.</summary>
public interface IDiaryRepository
{
    /// <summary>Newest first.</summary>
    Task<PagedResult<DiaryEntryView>> ListAsync(
        Guid userId, DiaryFilter filter, int page, int pageSize, CancellationToken cancellationToken);

    /// <summary>Newest first.</summary>
    Task<IReadOnlyList<DiaryEntryView>> ListForMovieAsync(Guid userId, Guid movieId, CancellationToken cancellationToken);

    Task<DiaryEntryView?> GetAsync(Guid userId, Guid entryId, CancellationToken cancellationToken);

    /// <summary>For changing the entry: changes are saved with <see cref="IUnitOfWork"/>.</summary>
    Task<DiaryEntry?> FindAsync(Guid userId, Guid entryId, CancellationToken cancellationToken);

    /// <summary>Saved with <see cref="IUnitOfWork"/>.</summary>
    void Add(DiaryEntry entry);

    /// <summary>For removing an entry found with <see cref="FindAsync"/>: saved with <see cref="IUnitOfWork"/>.</summary>
    void Remove(DiaryEntry entry);
}
