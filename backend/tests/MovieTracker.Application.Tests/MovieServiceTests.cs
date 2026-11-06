using Microsoft.Extensions.Logging.Abstractions;
using MovieTracker.Application.Movies;
using MovieTracker.Application.Tests.Fakes;
using MovieTracker.Application.Tmdb;
using MovieTracker.Domain.Movies;

namespace MovieTracker.Application.Tests;

public sealed class MovieServiceTests
{
    private const int Horror = FakeGenreRepository.Horror;
    private const int ScienceFiction = FakeGenreRepository.ScienceFiction;
    private const int Action = FakeGenreRepository.Action;

    private static readonly DateTimeOffset Start = new(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid Alice = Guid.NewGuid();
    private static readonly Guid Bob = Guid.NewGuid();

    private static readonly MovieDetails AlienDetails = new(
        "Alien", "Alien", new DateOnly(1979, 5, 25), 117, "In space no one can hear you scream.", "https://image.example/alien.jpg");

    private readonly FakeMovieRepository _movies = new();
    private readonly FakeGenreRepository _genres = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly StubTmdbClient _tmdb = new();
    private readonly TestClock _clock = new(Start);
    private readonly MovieService _service;

    public MovieServiceTests()
    {
        _service = new MovieService(_movies, _genres, _unitOfWork, _tmdb, _clock, NullLogger<MovieService>.Instance);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private Movie AddManualMovie(Guid owner, string title = "Alien", params int[] genreIds)
    {
        var movie = Movie.CreateManual(
            owner,
            AlienDetails with { Title = title },
            _genres.Genres.Where(genre => genreIds.Contains(genre.Id)),
            Start);
        _movies.Movies.Add(movie);
        return movie;
    }

    private Movie AddTmdbMovie(int tmdbId, string title = "Alien")
    {
        var movie = Movie.FromTmdb(tmdbId, AlienDetails with { Title = title }, [], Start);
        _movies.Movies.Add(movie);
        return movie;
    }

    private static TmdbSearchResult TmdbHit(int tmdbId, string title) =>
        new(tmdbId, title, title, new DateOnly(1979, 5, 25), null);

    // --- Listing

    [Fact]
    public async Task Lists_the_movies_of_the_user_with_the_query()
    {
        var query = new MovieListQuery("alien", GenreId: Horror, Year: 1979, MovieSort.ReleaseDate, Page: 2, PageSize: 10);
        var expected = new PagedResult<MovieSummary>([], 2, 10, 11);
        _movies.ListResult = expected;

        var result = await _service.ListAsync(Alice, query, Ct);

        Assert.Same(expected, result);
        Assert.Equal((Alice, query), _movies.LastList);
    }

    // --- Search

    [Fact]
    public async Task Search_lists_the_users_own_movies_before_TMDBs_results()
    {
        var own = AddManualMovie(Alice, "Alien Home Video");
        _tmdb.SearchResults.Add(TmdbHit(348, "Alien"));

        var result = await _service.SearchAsync(Alice, "alien", Ct);

        Assert.True(result.TmdbAvailable);
        Assert.Collection(
            result.Hits,
            hit => Assert.Equal((own.Id, (int?)null, "Alien Home Video"), (hit.MovieId, hit.TmdbId, hit.Title)),
            hit => Assert.Equal(((Guid?)null, (int?)348, "Alien"), (hit.MovieId, hit.TmdbId, hit.Title)));
    }

    [Fact]
    public async Task Search_does_not_show_other_users_manual_movies()
    {
        AddManualMovie(Bob, "Alien Home Video");

        var result = await _service.SearchAsync(Alice, "alien", Ct);

        Assert.Empty(result.Hits);
    }

    [Fact]
    public async Task Search_shows_at_most_ten_of_the_users_own_movies()
    {
        for (var i = 1; i <= 12; i++)
        {
            AddManualMovie(Alice, $"Alien {i:00}");
        }

        var result = await _service.SearchAsync(Alice, "alien", Ct);

        Assert.Equal(10, result.Hits.Count);
        Assert.Equal("Alien 10", result.Hits[^1].Title);
    }

    [Fact]
    public async Task Search_marks_TMDB_results_that_are_saved_already_with_their_movie()
    {
        var saved = AddTmdbMovie(348);
        _tmdb.SearchResults.AddRange([TmdbHit(348, "Alien"), TmdbHit(679, "Aliens")]);

        var result = await _service.SearchAsync(Alice, "alien", Ct);

        Assert.Equal([saved.Id, (Guid?)null], result.Hits.Select(hit => hit.MovieId));
    }

    [Fact]
    public async Task Search_sends_the_trimmed_query_to_TMDB()
    {
        await _service.SearchAsync(Alice, "  alien  ", Ct);

        Assert.Equal(["alien"], _tmdb.Searches);
    }

    [Fact]
    public async Task Search_only_finds_own_movies_if_TMDB_is_not_configured()
    {
        _tmdb.IsConfigured = false;
        var own = AddManualMovie(Alice, "Alien Home Video");

        var result = await _service.SearchAsync(Alice, "alien", Ct);

        Assert.False(result.TmdbAvailable);
        Assert.Equal(own.Id, Assert.Single(result.Hits).MovieId);
    }

    [Fact]
    public async Task Search_only_finds_own_movies_if_TMDB_fails()
    {
        _tmdb.IsUnavailable = true;
        var own = AddManualMovie(Alice, "Alien Home Video");

        var result = await _service.SearchAsync(Alice, "alien", Ct);

        Assert.False(result.TmdbAvailable);
        Assert.Equal(own.Id, Assert.Single(result.Hits).MovieId);
    }

    // --- Importing a movie from TMDB

    [Fact]
    public async Task Import_saves_the_movie_from_TMDB_with_its_genres()
    {
        _tmdb.Movies[348] = new TmdbMovie(348, AlienDetails, [Horror, ScienceFiction]);

        var result = await _service.ImportTmdbMovieAsync(Alice, 348, Ct);

        Assert.Equal(ImportTmdbMovieStatus.Imported, result.Status);
        var saved = Assert.Single(_movies.Movies);
        Assert.Equal((348, (Guid?)null, "Alien", Start), (saved.TmdbId, saved.OwnerId, saved.Title, saved.CreatedAt));
        Assert.Equal([Horror, ScienceFiction], saved.Genres.Select(genre => genre.Id).Order());
        Assert.Equal(saved.Id, result.Movie!.Id);
        Assert.Equal(117, result.Movie.RuntimeMinutes);
    }

    [Fact]
    public async Task Import_leaves_out_genres_that_are_not_seeded()
    {
        _tmdb.Movies[679] = new TmdbMovie(679, AlienDetails, [Action, ScienceFiction, 999_999]);

        await _service.ImportTmdbMovieAsync(Alice, 679, Ct);

        Assert.Equal([Action, ScienceFiction], Assert.Single(_movies.Movies).Genres.Select(genre => genre.Id).Order());
    }

    [Fact]
    public async Task Import_returns_a_movie_that_is_saved_already_without_asking_TMDB()
    {
        // Also when TMDB is not available at all: the movie is there, nothing needs to be fetched.
        _tmdb.IsConfigured = false;
        var saved = AddTmdbMovie(348);

        var result = await _service.ImportTmdbMovieAsync(Bob, 348, Ct);

        Assert.Equal(ImportTmdbMovieStatus.AlreadyImported, result.Status);
        Assert.Equal(saved.Id, result.Movie!.Id);
        Assert.Equal(0, _tmdb.GetMovieCalls);
    }

    [Fact]
    public async Task Import_is_refused_without_TMDB_access()
    {
        _tmdb.IsConfigured = false;

        var result = await _service.ImportTmdbMovieAsync(Alice, 348, Ct);

        Assert.Equal(ImportTmdbMovieStatus.TmdbNotConfigured, result.Status);
        Assert.Empty(_movies.Movies);
    }

    [Fact]
    public async Task Import_reports_that_TMDB_is_unavailable()
    {
        _tmdb.IsUnavailable = true;

        var result = await _service.ImportTmdbMovieAsync(Alice, 348, Ct);

        Assert.Equal(ImportTmdbMovieStatus.TmdbUnavailable, result.Status);
        Assert.Empty(_movies.Movies);
    }

    [Fact]
    public async Task Import_reports_a_movie_that_TMDB_does_not_know()
    {
        var result = await _service.ImportTmdbMovieAsync(Alice, 12345, Ct);

        Assert.Equal(ImportTmdbMovieStatus.UnknownTmdbMovie, result.Status);
        Assert.Empty(_movies.Movies);
    }

    [Fact]
    public async Task Import_returns_the_movie_that_somebody_else_saved_at_the_same_moment()
    {
        _tmdb.Movies[348] = new TmdbMovie(348, AlienDetails, [Horror]);
        Movie? concurrent = null;
        _movies.BeforeAddTmdbMovie = () => concurrent = AddTmdbMovie(348);

        var result = await _service.ImportTmdbMovieAsync(Alice, 348, Ct);

        Assert.Equal(ImportTmdbMovieStatus.AlreadyImported, result.Status);
        Assert.Equal(concurrent!.Id, result.Movie!.Id);
        Assert.Single(_movies.Movies);
    }

    // --- Reading

    [Fact]
    public async Task Get_returns_the_movie_to_those_who_can_see_it()
    {
        var own = AddManualMovie(Alice);
        var tmdb = AddTmdbMovie(348, "Alien (TMDB)");

        Assert.Equal(own.Id, (await _service.GetAsync(Alice, own.Id, Ct))!.Id);
        Assert.Equal(tmdb.Id, (await _service.GetAsync(Bob, tmdb.Id, Ct))!.Id);
        Assert.Null(await _service.GetAsync(Bob, own.Id, Ct));
    }

    // --- Adding a manual movie

    [Fact]
    public async Task Create_adds_a_manual_movie_that_belongs_to_the_user()
    {
        var result = await _service.CreateAsync(Alice, AlienDetails, [Horror, ScienceFiction], Ct);

        Assert.Equal(SaveMovieStatus.Saved, result.Status);
        var saved = Assert.Single(_movies.Movies);
        Assert.Equal((Alice, (int?)null, Start), (saved.OwnerId, saved.TmdbId, saved.CreatedAt));
        Assert.Equal([Horror, ScienceFiction], saved.Genres.Select(genre => genre.Id).Order());
        Assert.Equal(saved.Id, result.Movie!.Id);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Create_assigns_a_genre_that_is_given_twice_only_once()
    {
        await _service.CreateAsync(Alice, AlienDetails, [Horror, Horror], Ct);

        Assert.Single(Assert.Single(_movies.Movies).Genres);
    }

    [Fact]
    public async Task Create_rejects_unknown_genres_and_names_them_once_in_the_order_given()
    {
        var result = await _service.CreateAsync(Alice, AlienDetails, [Horror, 222, 111, 222], Ct);

        Assert.Equal(SaveMovieStatus.UnknownGenres, result.Status);
        Assert.Equal([222, 111], result.UnknownGenreIds);
        Assert.Empty(_movies.Movies);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    // --- Changing a manual movie

    [Fact]
    public async Task Update_changes_a_manual_movie_and_its_genres()
    {
        var movie = AddManualMovie(Alice, "Alien", Horror);
        var changed = AlienDetails with { Title = "Alien: Director's Cut", RuntimeMinutes = 116 };

        var result = await _service.UpdateAsync(Alice, movie.Id, changed, [ScienceFiction, Action], Ct);

        Assert.Equal(SaveMovieStatus.Saved, result.Status);
        Assert.Equal(("Alien: Director's Cut", 116), (movie.Title, movie.RuntimeMinutes));
        Assert.Equal([Action, ScienceFiction], movie.Genres.Select(genre => genre.Id).Order());
        Assert.Equal("Alien: Director's Cut", result.Movie!.Title);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Update_does_not_find_other_users_manual_movies()
    {
        var movie = AddManualMovie(Bob, "Alien");

        var result = await _service.UpdateAsync(Alice, movie.Id, AlienDetails with { Title = "Mine now" }, [Horror], Ct);

        Assert.Equal(SaveMovieStatus.NotFound, result.Status);
        Assert.Equal("Alien", movie.Title);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Update_does_not_find_unknown_movies()
    {
        var result = await _service.UpdateAsync(Alice, Guid.NewGuid(), AlienDetails, [Horror], Ct);

        Assert.Equal(SaveMovieStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task Update_refuses_movies_from_TMDB()
    {
        var movie = AddTmdbMovie(348, "Alien");

        var result = await _service.UpdateAsync(Alice, movie.Id, AlienDetails with { Title = "Changed" }, [Horror], Ct);

        Assert.Equal(SaveMovieStatus.ReadOnly, result.Status);
        Assert.Equal("Alien", movie.Title);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Update_rejects_unknown_genres_and_leaves_the_movie_as_it_was()
    {
        var movie = AddManualMovie(Alice, "Alien", Horror);

        var result = await _service.UpdateAsync(Alice, movie.Id, AlienDetails with { Title = "Changed" }, [ScienceFiction, 111], Ct);

        Assert.Equal(SaveMovieStatus.UnknownGenres, result.Status);
        Assert.Equal([111], result.UnknownGenreIds);
        Assert.Equal("Alien", movie.Title);
        Assert.Equal([Horror], movie.Genres.Select(genre => genre.Id));
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    // --- Deleting a manual movie

    [Fact]
    public async Task Delete_removes_a_manual_movie()
    {
        var movie = AddManualMovie(Alice);

        var result = await _service.DeleteAsync(Alice, movie.Id, Ct);

        Assert.Equal(DeleteMovieResult.Deleted, result);
        Assert.Empty(_movies.Movies);
    }

    [Fact]
    public async Task Delete_does_not_find_other_users_manual_movies()
    {
        var movie = AddManualMovie(Bob);

        var result = await _service.DeleteAsync(Alice, movie.Id, Ct);

        Assert.Equal(DeleteMovieResult.NotFound, result);
        Assert.Contains(movie, _movies.Movies);
    }

    [Fact]
    public async Task Delete_refuses_movies_from_TMDB()
    {
        var movie = AddTmdbMovie(348);

        var result = await _service.DeleteAsync(Alice, movie.Id, Ct);

        Assert.Equal(DeleteMovieResult.ReadOnly, result);
        Assert.Contains(movie, _movies.Movies);
    }

    [Fact]
    public async Task Delete_refuses_a_movie_that_is_in_the_diary_so_the_history_is_not_lost()
    {
        var movie = AddManualMovie(Alice);
        _movies.MoviesInDiary.Add(movie.Id);

        var result = await _service.DeleteAsync(Alice, movie.Id, Ct);

        Assert.Equal(DeleteMovieResult.HasDiaryEntries, result);
        Assert.Contains(movie, _movies.Movies);
    }
}
