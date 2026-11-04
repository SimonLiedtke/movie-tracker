using MovieTracker.Domain.Movies;

namespace MovieTracker.Domain.Diary;

public sealed class DiaryEntry
{
    private DiaryEntry()
    {
        // Required by EF Core.
    }

    public DiaryEntry(Guid userId, DiaryEntryDetails details, DateTimeOffset createdAt)
    {
        Id = Guid.CreateVersion7(createdAt);
        UserId = userId;
        CreatedAt = createdAt;
        Update(details);
    }

    public Guid Id { get; private set; }

    /// <summary>The owner. Only they can see and change the entry.</summary>
    public Guid UserId { get; private set; }

    public Guid MovieId { get; private set; }

    public Movie Movie { get; private set; } = null!;

    public DateOnly WatchedOn { get; private set; }

    public Rating? Rating { get; private set; }

    public string? Review { get; private set; }

    public bool IsRewatch { get; private set; }

    public bool Liked { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public void Update(DiaryEntryDetails details)
    {
        ArgumentNullException.ThrowIfNull(details);

        MovieId = details.MovieId;
        WatchedOn = details.WatchedOn;
        Rating = details.Rating;
        Review = details.Review;
        IsRewatch = details.IsRewatch;
        Liked = details.Liked;
    }
}
