using MovieTracker.Api.Contracts;
using MovieTracker.Application.Letterboxd;

namespace MovieTracker.Api.Imports;

/// <summary>
/// One Letterboxd import. Created by the upload, worked on by <see cref="ILetterboxdImporter"/> on the background
/// worker and read by the polling client, hence the lock.
/// </summary>
public sealed class LetterboxdImportJob(Guid userId, LetterboxdExport export)
{
    private readonly Lock _lock = new();
    private ImportStatus _status = ImportStatus.Running;
    private ImportResultResponse? _result;
    private string? _error;
    private DateTimeOffset? _finishedAt;

    public Guid Id { get; } = Guid.NewGuid();

    public Guid UserId { get; } = userId;

    public LetterboxdExport Export { get; } = export;

    /// <summary>Filled by the importer while it runs.</summary>
    public LetterboxdImportProgress Progress { get; } = new();

    public bool IsRunning
    {
        get
        {
            lock (_lock)
            {
                return _status == ImportStatus.Running;
            }
        }
    }

    public DateTimeOffset? FinishedAt
    {
        get
        {
            lock (_lock)
            {
                return _finishedAt;
            }
        }
    }

    public void Complete(LetterboxdImportResult result, DateTimeOffset now)
    {
        lock (_lock)
        {
            _result = ToResponse(result);
            _status = ImportStatus.Completed;
            _finishedAt = now;
        }
    }

    public void Fail(string error, DateTimeOffset now)
    {
        lock (_lock)
        {
            _error = error;
            _status = ImportStatus.Failed;
            _finishedAt = now;
        }
    }

    public ImportJobResponse ToResponse()
    {
        lock (_lock)
        {
            return new ImportJobResponse(Id, _status, Progress.FilmsTotal, Progress.FilmsProcessed, _result, _error);
        }
    }

    private static ImportResultResponse ToResponse(LetterboxdImportResult result) => new(
        result.DiaryEntriesImported,
        result.DiaryEntriesSkipped,
        result.WatchlistItemsImported,
        result.WatchlistItemsSkipped,
        result.ManualFilms.Select(ToResponse).ToList(),
        result.FailedFilms.Select(ToResponse).ToList(),
        result.Warnings);

    private static ImportFilmResponse ToResponse(LetterboxdFilm film) => new(film.Name, film.Year);
}
