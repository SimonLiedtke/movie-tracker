using System.Threading.Channels;
using MovieTracker.Application.Letterboxd;

namespace MovieTracker.Api.Imports;

/// <summary>
/// Hands uploaded exports to <see cref="LetterboxdImportWorker"/> and remembers the jobs so clients can ask for their
/// progress. Jobs live in memory only: after a restart they are gone, and uploading the file again picks up where
/// the import left off, because already imported entries are skipped.
/// </summary>
public sealed class LetterboxdImportQueue
{
    private static readonly TimeSpan KeepFinishedJobs = TimeSpan.FromHours(1);

    private readonly Lock _lock = new();
    private readonly Dictionary<Guid, LetterboxdImportJob> _jobs = [];
    private readonly Channel<LetterboxdImportJob> _channel = Channel.CreateUnbounded<LetterboxdImportJob>();

    internal ChannelReader<LetterboxdImportJob> Reader => _channel.Reader;

    /// <summary>Returns null if the user already has an import that has not finished.</summary>
    public LetterboxdImportJob? TryEnqueue(Guid userId, LetterboxdExport export, DateTimeOffset now)
    {
        lock (_lock)
        {
            var expired = _jobs.Values.Where(job => job.FinishedAt < now - KeepFinishedJobs).Select(job => job.Id).ToList();
            foreach (var id in expired)
            {
                _jobs.Remove(id);
            }

            if (_jobs.Values.Any(job => job.UserId == userId && job.IsRunning))
            {
                return null;
            }

            var newJob = new LetterboxdImportJob(userId, export);
            _jobs.Add(newJob.Id, newJob);
            _channel.Writer.TryWrite(newJob);
            return newJob;
        }
    }

    /// <summary>Only the user's own jobs are found; other users' jobs do not exist for them.</summary>
    public LetterboxdImportJob? Find(Guid userId, Guid jobId)
    {
        lock (_lock)
        {
            return _jobs.TryGetValue(jobId, out var job) && job.UserId == userId ? job : null;
        }
    }
}
