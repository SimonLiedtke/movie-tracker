using MovieTracker.Domain.Movies;

namespace MovieTracker.Domain.Tests;

public sealed class MovieTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);
    private static readonly Genre Horror = new(27, "Horror");
    private static readonly Genre ScienceFiction = new(878, "Science Fiction");

    private static MovieDetails Details(string title = "Alien", int? runtimeMinutes = 117) =>
        new(title, "Alien", new DateOnly(1979, 5, 25), runtimeMinutes, "In space…", "https://example.com/alien.jpg", 348);

    [Fact]
    public void Creates_a_movie_from_its_details()
    {
        var movie = new Movie(Details(), [Horror, ScienceFiction], CreatedAt);

        Assert.Equal("Alien", movie.Title);
        Assert.Equal(new DateOnly(1979, 5, 25), movie.ReleaseDate);
        Assert.Equal(117, movie.RuntimeMinutes);
        Assert.Equal(348, movie.TmdbId);
        Assert.Equal(CreatedAt, movie.CreatedAt);
        Assert.Equal([27, 878], movie.Genres.Select(genre => genre.Id).Order());
    }

    [Fact]
    public void Uses_a_time_ordered_id()
    {
        var movie = new Movie(Details(), [], CreatedAt);

        Assert.Equal(7, movie.Id.Version);
    }

    [Fact]
    public void Trims_the_title()
    {
        var movie = new Movie(Details(title: "  Alien  "), [], CreatedAt);

        Assert.Equal("Alien", movie.Title);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Rejects_a_blank_title(string title)
    {
        Assert.Throws<ArgumentException>(() => new Movie(Details(title: title), [], CreatedAt));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Rejects_a_runtime_that_is_not_positive(int runtimeMinutes)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new Movie(Details(runtimeMinutes: runtimeMinutes), [], CreatedAt));
    }

    [Fact]
    public void Ignores_duplicate_genres()
    {
        var movie = new Movie(Details(), [Horror, new Genre(27, "Horror")], CreatedAt);

        Assert.Single(movie.Genres);
    }

    [Fact]
    public void Update_replaces_details_and_genres()
    {
        var movie = new Movie(Details(), [Horror], CreatedAt);

        movie.Update(new MovieDetails("Aliens", null, null, null, null, null, null), [ScienceFiction]);

        Assert.Equal("Aliens", movie.Title);
        Assert.Null(movie.OriginalTitle);
        Assert.Null(movie.ReleaseDate);
        Assert.Null(movie.TmdbId);
        Assert.Equal(ScienceFiction, Assert.Single(movie.Genres));
        Assert.Equal(CreatedAt, movie.CreatedAt);
    }
}
