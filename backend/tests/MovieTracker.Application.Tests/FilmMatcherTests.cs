using MovieTracker.Application.Letterboxd;
using MovieTracker.Application.Tmdb;

namespace MovieTracker.Application.Tests;

public sealed class FilmMatcherTests
{
    private static readonly LetterboxdFilm Alien = new("Alien", 1979);

    [Theory]
    [InlineData("Alien", "Alien")]
    [InlineData("ALIEN", "alien")]
    [InlineData("Amélie", "Amelie")]
    [InlineData("WALL·E", "Wall-E")]
    [InlineData("  Spaced   Out ", "Spaced Out")]
    public void Titles_are_compared_loosely(string letterboxdName, string tmdbTitle)
    {
        Assert.Equal(0, FilmMatcher.Match(new LetterboxdFilm(letterboxdName, 2005), tmdbTitle, null, new DateOnly(2005, 6, 10)));
    }

    [Fact]
    public void An_ampersand_is_not_the_word_and()
    {
        // Known limit: such a film is not found on TMDB and added as a manual movie instead of a wrong guess.
        var film = new LetterboxdFilm("Mr. & Mrs. Smith", 2005);

        Assert.Null(FilmMatcher.Match(film, "Mr. and Mrs. Smith", null, new DateOnly(2005, 6, 10)));
    }

    [Fact]
    public void A_different_title_does_not_match()
    {
        Assert.Null(FilmMatcher.Match(Alien, "Aliens", null, new DateOnly(1979, 5, 25)));
    }

    [Fact]
    public void The_original_title_matches_too()
    {
        // Letterboxd calls it "Spirited Away", TMDB (in another language) has the Japanese original.
        var film = new LetterboxdFilm("Spirited Away", 2001);

        Assert.Equal(0, FilmMatcher.Match(film, "Chihiros Reise ins Zauberland", "Spirited Away", new DateOnly(2001, 7, 20)));
    }

    [Theory]
    [InlineData(1979, 0)]
    [InlineData(1978, 1)]
    [InlineData(1980, 1)]
    [InlineData(1977, null)]
    [InlineData(1981, null)]
    public void The_release_year_may_be_one_year_off(int tmdbYear, int? expected)
    {
        Assert.Equal(expected, FilmMatcher.Match(Alien, "Alien", null, new DateOnly(tmdbYear, 6, 1)));
    }

    [Fact]
    public void Without_a_year_the_title_decides()
    {
        var film = new LetterboxdFilm("Alien", null);

        Assert.Equal(0, FilmMatcher.Match(film, "Alien", null, new DateOnly(1979, 5, 25)));
        Assert.Equal(0, FilmMatcher.Match(film, "Alien", null, null));
    }

    [Fact]
    public void A_movie_without_release_date_does_not_match_a_film_with_a_year()
    {
        Assert.Null(FilmMatcher.Match(Alien, "Alien", null, null));
    }

    [Fact]
    public void FindBest_prefers_the_exact_year_over_a_better_ranked_result()
    {
        var results = new[]
        {
            Result(1, "Alien", new DateOnly(1980, 1, 1)),
            Result(2, "Alien", new DateOnly(1979, 5, 25)),
        };

        Assert.Equal(2, FilmMatcher.FindBest(Alien, results)?.TmdbId);
    }

    [Fact]
    public void FindBest_follows_TMDBs_order_between_equally_good_results()
    {
        var results = new[]
        {
            Result(1, "Alien", new DateOnly(1979, 5, 25)),
            Result(2, "Alien", new DateOnly(1979, 12, 1)),
        };

        Assert.Equal(1, FilmMatcher.FindBest(Alien, results)?.TmdbId);
    }

    [Fact]
    public void FindBest_returns_nothing_if_no_result_is_the_film()
    {
        var results = new[] { Result(1, "Aliens", new DateOnly(1986, 7, 18)), Result(2, "Alien", new DateOnly(2000, 1, 1)) };

        Assert.Null(FilmMatcher.FindBest(Alien, results));
        Assert.Null(FilmMatcher.FindBest(Alien, []));
    }

    private static TmdbSearchResult Result(int id, string title, DateOnly releaseDate) =>
        new(id, title, null, releaseDate, null);
}
