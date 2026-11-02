namespace MovieTracker.Domain.Movies;

public sealed class Movie
{
    private readonly List<Genre> _genres = [];

    private Movie()
    {
        // Required by EF Core.
    }

    public Movie(MovieDetails details, IEnumerable<Genre> genres, DateTimeOffset createdAt)
    {
        Id = Guid.CreateVersion7(createdAt);
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

    public int? TmdbId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public IReadOnlyCollection<Genre> Genres => _genres.AsReadOnly();

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
        TmdbId = details.TmdbId;

        _genres.Clear();
        _genres.AddRange(genres.DistinctBy(genre => genre.Id));
    }
}
