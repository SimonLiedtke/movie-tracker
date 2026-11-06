namespace MovieTracker.Application.Letterboxd;

/// <summary>Saves the diary and watchlist of a Letterboxd export for a user.</summary>
public interface ILetterboxdImporter
{
    /// <param name="progress">Filled while the import runs, so the caller can show how far it is.</param>
    /// <exception cref="OperationCanceledException">The import was cancelled; nothing of the diary and watchlist is saved then.</exception>
    Task<LetterboxdImportResult> ImportAsync(
        Guid userId, LetterboxdExport export, LetterboxdImportProgress progress, CancellationToken cancellationToken);
}
