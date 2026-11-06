using MovieTracker.Api.Contracts;
using MovieTracker.Infrastructure.Letterboxd;

namespace MovieTracker.Api.Imports;

/// <summary>
/// One Letterboxd import. Created by the upload, filled with progress by <see cref="LetterboxdImporter"/> on the
/// background worker and read by the polling client, hence the lock.
/// </summary>
public sealed class LetterboxdImportJob(Guid userId, LetterboxdExport export)
{
    private readonly Lock _lock = new();
    private int _filmsTotal;
    private int _filmsProcessed;
    private ImportStatus _status = ImportStatus.Running;
    private ImportResultResponse? _result;
    private string? _error;
    private DateTimeOffset? _finishedAt;

    public Guid Id { get; } = Guid.NewGuid();

    public Guid UserId { get; } = userId;

    public LetterboxdExport Export { get; } = export;

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

    public void SetFilmsTotal(int total)
    {
        lock (_lock)
        {
            _filmsTotal = total;
        }
    }

    public void FilmProcessed()
    {
        lock (_lock)
        {
            _filmsProcessed++;
        }
    }

    public void Complete(ImportResultResponse result, DateTimeOffset now)
    {
        lock (_lock)
        {
            _result = result;
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
            return new ImportJobResponse(Id, _status, _filmsTotal, _filmsProcessed, _result, _error);
        }
    }
}
