namespace MovieTracker.Api.Imports;

/// <summary>
/// Runs the queued Letterboxd imports, one after the other: a big import makes hundreds of TMDB requests, which is too long
/// for a web request, and one at a time keeps the load on TMDB predictable.
/// </summary>
internal sealed class LetterboxdImportWorker(
    LetterboxdImportQueue queue,
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<LetterboxdImportWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var job in queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var importer = scope.ServiceProvider.GetRequiredService<LetterboxdImporter>();
                var result = await importer.ImportAsync(job, stoppingToken);
                job.Complete(result, timeProvider.GetUtcNow());
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                job.Fail("The server was stopped during the import. Upload the file again to continue.", timeProvider.GetUtcNow());
                throw;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Letterboxd import {JobId} failed", job.Id);
                job.Fail(
                    "The import failed unexpectedly. Upload the file again to retry; what was imported already is skipped.",
                    timeProvider.GetUtcNow());
            }
        }
    }
}
