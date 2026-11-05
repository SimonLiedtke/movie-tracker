namespace MovieTracker.Domain.Movies;

/// <summary>
/// A movie is one of two kinds:
/// <list type="bullet">
/// <item>From TMDB (<see cref="TmdbId"/> is set): shared by all users, its data comes from TMDB.</item>
/// <item>Added manually (<see cref="OwnerId"/> is set): private to the user who added it, who can edit it.</item>
/// </list>
/// </summary>
public sealed class Movie
{
    private readonly List<Genre> _genres = [];

    private Movie()
    {
        // Required by EF Core.
    }

    private Movie(int? tmdbId, Guid? ownerId, MovieDetails details, IEnumerable<Genre> genres, DateTimeOffset createdAt)
    {
        Id = Guid.CreateVersion7(createdAt);
        TmdbId = tmdbId;
        OwnerId = ownerId;
        CreatedAt = createdAt;
        Update(details, genres);
    }

    public Guid Id { get; private set; }

    public string Title { get; private set; } = null!;

    public string? OriginalTitle { get; private set; }

    public DateOnly? ReleaseDate { get; private set; }

    public int? RuntimeMinutes { get; private set; }

    public string? Overview { get; private set; }

    public string? PosterUrl { get; private set; }

    /// <summary>Set for movies from TMDB.</summary>
    public int? TmdbId { get; private set; }

    /// <summary>Set for manually added movies: the user who added it and is the only one who sees it.</summary>
    public Guid? OwnerId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public IReadOnlyCollection<Genre> Genres => _genres.AsReadOnly();

    public bool IsManual => OwnerId is not null;

    public static Movie FromTmdb(int tmdbId, MovieDetails details, IEnumerable<Genre> genres, DateTimeOffset createdAt)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(tmdbId);
        return new Movie(tmdbId, ownerId: null, details, genres, createdAt);
    }

    public static Movie CreateManual(Guid ownerId, MovieDetails details, IEnumerable<Genre> genres, DateTimeOffset createdAt)
    {
        if (ownerId == Guid.Empty)
        {
            throw new ArgumentException("A manually added movie needs an owner.", nameof(ownerId));
        }

        return new Movie(tmdbId: null, ownerId, details, genres, createdAt);
    }

    /// <summary>Movies from TMDB are visible to everyone, manually added movies only to their owner.</summary>
    public bool IsVisibleTo(Guid userId) => OwnerId is null || OwnerId == userId;

    public void Update(MovieDetails details, IEnumerable<Genre> genres)
    {
        ArgumentNullException.ThrowIfNull(details);
        ArgumentNullException.ThrowIfNull(genres);
        ArgumentException.ThrowIfNullOrWhiteSpace(details.Title);

        if (details.RuntimeMinutes is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(details), "Runtime must be positive.");
        }

        Title = details.Title.Trim();
        OriginalTitle = details.OriginalTitle;
        ReleaseDate = details.ReleaseDate;
        RuntimeMinutes = details.RuntimeMinutes;
        Overview = details.Overview;
        PosterUrl = details.PosterUrl;

        _genres.Clear();
        _genres.AddRange(genres.DistinctBy(genre => genre.Id));
    }
}
