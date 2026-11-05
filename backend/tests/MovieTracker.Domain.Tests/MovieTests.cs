using MovieTracker.Domain.Movies;

namespace MovieTracker.Domain.Tests;

public sealed class MovieTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid Alice = Guid.CreateVersion7();
    private static readonly Guid Bob = Guid.CreateVersion7();
    private static readonly Genre Horror = new(27, "Horror");
    private static readonly Genre ScienceFiction = new(878, "Science Fiction");

    private static MovieDetails Details(string title = "Alien", int? runtimeMinutes = 117) =>
        new(title, "Alien", new DateOnly(1979, 5, 25), runtimeMinutes, "In space…", "https://example.com/alien.jpg");

    private static Movie Manual(MovieDetails? details = null, params Genre[] genres) =>
        Movie.CreateManual(Alice, details ?? Details(), genres, CreatedAt);

    [Fact]
    public void Creates_a_movie_from_its_details()
    {
        var movie = Manual(Details(), Horror, ScienceFiction);

        Assert.Equal("Alien", movie.Title);
        Assert.Equal(new DateOnly(1979, 5, 25), movie.ReleaseDate);
        Assert.Equal(117, movie.RuntimeMinutes);
        Assert.Equal(CreatedAt, movie.CreatedAt);
        Assert.Equal([27, 878], movie.Genres.Select(genre => genre.Id).Order());
    }

    [Fact]
    public void A_manual_movie_belongs_to_its_owner_only()
    {
        var movie = Manual();

        Assert.True(movie.IsManual);
        Assert.Equal(Alice, movie.OwnerId);
        Assert.Null(movie.TmdbId);
        Assert.True(movie.IsVisibleTo(Alice));
        Assert.False(movie.IsVisibleTo(Bob));
    }

    [Fact]
    public void A_tmdb_movie_is_shared_by_everyone()
    {
        var movie = Movie.FromTmdb(348, Details(), [Horror], CreatedAt);

        Assert.False(movie.IsManual);
        Assert.Equal(348, movie.TmdbId);
        Assert.Null(movie.OwnerId);
        Assert.True(movie.IsVisibleTo(Alice));
        Assert.True(movie.IsVisibleTo(Bob));
    }

    [Fact]
    public void A_manual_movie_needs_an_owner()
    {
        Assert.Throws<ArgumentException>(() => Movie.CreateManual(Guid.Empty, Details(), [], CreatedAt));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void A_tmdb_movie_needs_a_positive_tmdb_id(int tmdbId)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Movie.FromTmdb(tmdbId, Details(), [], CreatedAt));
    }

    [Fact]
    public void Uses_a_time_ordered_id()
    {
        Assert.Equal(7, Manual().Id.Version);
    }

    [Fact]
    public void Trims_the_title()
    {
        Assert.Equal("Alien", Manual(Details(title: "  Alien  ")).Title);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Rejects_a_blank_title(string title)
    {
        Assert.Throws<ArgumentException>(() => Manual(Details(title: title)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Rejects_a_runtime_that_is_not_positive(int runtimeMinutes)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Manual(Details(runtimeMinutes: runtimeMinutes)));
    }

    [Fact]
    public void Ignores_duplicate_genres()
    {
        var movie = Manual(Details(), Horror, new Genre(27, "Horror"));

        Assert.Single(movie.Genres);
    }

    [Fact]
    public void Update_replaces_details_and_genres_but_keeps_the_owner()
    {
        var movie = Manual(Details(), Horror);

        movie.Update(new MovieDetails("Aliens", null, null, null, null, null), [ScienceFiction]);

        Assert.Equal("Aliens", movie.Title);
        Assert.Null(movie.OriginalTitle);
        Assert.Null(movie.ReleaseDate);
        Assert.Equal(ScienceFiction, Assert.Single(movie.Genres));
        Assert.Equal(CreatedAt, movie.CreatedAt);
        Assert.Equal(Alice, movie.OwnerId);
    }
}
