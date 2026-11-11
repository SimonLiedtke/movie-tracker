namespace MovieTracker.Domain.Activity;

/// <summary>What a user did that changed the database.</summary>
public enum ActivityType
{
    DiaryEntryAdded,
    DiaryEntryUpdated,
    DiaryEntryDeleted,
    WatchlistItemAdded,
    WatchlistNoteChanged,
    WatchlistTagsChanged,
    WatchlistItemRemoved,

    /// <summary>A movie that TMDB knows was made available (imported from TMDB).</summary>
    MovieImported,

    /// <summary>A movie was added manually.</summary>
    MovieAdded,

    MovieUpdated,
    MovieDeleted,
    LetterboxdImported,
}

/// <summary>
/// One line of a user's activity log: something they did that modified the database, and when. Entries are only added,
/// never changed. They are private to the user.
/// </summary>
public sealed class ActivityLogEntry
{
    public const int MaxMovieTitleLength = 300;
    public const int MaxDetailLength = 1000;

    private ActivityLogEntry()
    {
        // Required by EF Core.
    }

    private ActivityLogEntry(
        Guid userId, ActivityType type, DateTimeOffset occurredAt, Guid? movieId, string? movieTitle, string? detail)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("An activity belongs to a user.", nameof(userId));
        }

        Id = Guid.CreateVersion7(occurredAt);
        UserId = userId;
        Type = type;
        OccurredAt = occurredAt;
        MovieId = movieId;
        MovieTitle = Truncate(movieTitle, MaxMovieTitleLength);
        Detail = Truncate(detail, MaxDetailLength);
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public ActivityType Type { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    /// <summary>The movie the action was about. Null if the action was not about one movie, or the movie was deleted since.</summary>
    public Guid? MovieId { get; private set; }

    /// <summary>The movie's title when the action happened, so the log still reads well after the movie was deleted.</summary>
    public string? MovieTitle { get; private set; }

    /// <summary>What exactly happened, in a few words, e.g. the rating or the note. Null if the type says it all.</summary>
    public string? Detail { get; private set; }

    /// <summary>An action on a movie.</summary>
    /// <param name="movieId">Pass null for an action that deletes the movie: nothing is left to refer to.</param>
    public static ActivityLogEntry ForMovie(
        Guid userId, ActivityType type, DateTimeOffset occurredAt, Guid? movieId, string movieTitle, string? detail = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(movieTitle);
        return new ActivityLogEntry(userId, type, occurredAt, movieId, movieTitle, detail);
    }

    /// <summary>An action that is not about one movie.</summary>
    public static ActivityLogEntry General(Guid userId, ActivityType type, DateTimeOffset occurredAt, string? detail = null) =>
        new(userId, type, occurredAt, movieId: null, movieTitle: null, detail);

    private static string? Truncate(string? text, int maxLength) =>
        text is null || text.Length <= maxLength ? text : string.Concat(text.AsSpan(0, maxLength - 1), "…");
}
