using Microsoft.Extensions.Logging.Abstractions;
using MovieTracker.Application.Activity;
using MovieTracker.Application.Diary;
using MovieTracker.Application.Movies;
using MovieTracker.Application.Tests.Fakes;
using MovieTracker.Application.Tmdb;
using MovieTracker.Application.Watchlist;
using MovieTracker.Domain.Activity;
using MovieTracker.Domain.Diary;
using MovieTracker.Domain.Movies;

namespace MovieTracker.Application.Tests;

/// <summary>Every use case that changes something writes a line of the user's activity log, and only then.</summary>
public sealed class ActivityLogTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid Alice = Guid.NewGuid();
    private static readonly Guid Bob = Guid.NewGuid();

    private readonly FakeMovieRepository _movies = new();
    private readonly FakeGenreRepository _genres = new();
    private readonly FakeActivityLogRepository _log = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly StubTmdbClient _tmdb = new();
    private readonly TestClock _clock = new(Start);
    private readonly FakeDiaryRepository _diary;
    private readonly FakeWatchlistRepository _watchlist;
    private readonly DiaryService _diaryService;
    private readonly WatchlistService _watchlistService;
    private readonly MovieService _movieService;
    private readonly ActivityLogService _service;

    public ActivityLogTests()
    {
        _diary = new FakeDiaryRepository(_movies);
        _watchlist = new FakeWatchlistRepository(_movies);
        _diaryService = new DiaryService(_diary, _watchlist, _movies, _log, _unitOfWork, _clock);
        _watchlistService = new WatchlistService(_watchlist, _movies, _log, _unitOfWork, _clock);
        _movieService = new MovieService(_movies, _genres, _log, _unitOfWork, _tmdb, _clock, NullLogger<MovieService>.Instance);
        _service = new ActivityLogService(_log);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private Movie AddTmdbMovie(string title = "Alien", int tmdbId = 348)
    {
        var movie = Movie.FromTmdb(tmdbId, new MovieDetails(title, null, new DateOnly(1979, 5, 25), 117, null, null), [], Start);
        _movies.Movies.Add(movie);
        return movie;
    }

    private static DiaryEntryDetails Details(
        Movie movie, string watchedOn = "2026-10-31", int? rating = 8, string? review = null, bool isRewatch = false, bool liked = false) =>
        new(movie.Id, DateOnly.Parse(watchedOn), rating is { } halfStars ? new Rating(halfStars) : null, review, isRewatch, liked);

    private static ActivityLogEntry Single(IEnumerable<ActivityLogEntry> entries, ActivityType type)
    {
        var entry = Assert.Single(entries);
        Assert.Equal(type, entry.Type);
        return entry;
    }

    // --- Diary

    [Fact]
    public async Task Logging_a_viewing_is_logged_with_the_movie_and_the_time()
    {
        var movie = AddTmdbMovie("Alien");

        await _diaryService.CreateAsync(Alice, Details(movie, "2026-10-31", rating: 9, liked: true), Ct);

        var entry = Single(_log.Entries, ActivityType.DiaryEntryAdded);
        Assert.Equal((Alice, movie.Id, "Alien", Start), (entry.UserId, entry.MovieId, entry.MovieTitle, entry.OccurredAt));
        Assert.Equal("Watched on 2026-10-31 · 4.5 stars · liked", entry.Detail);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Logging_a_viewing_says_that_the_movie_was_taken_off_the_watchlist()
    {
        var movie = AddTmdbMovie();
        await _watchlistService.PutAsync(Alice, movie.Id, null, Ct);
        _log.Entries.Clear();

        await _diaryService.CreateAsync(Alice, Details(movie, rating: null), Ct);

        var entry = Single(_log.Entries, ActivityType.DiaryEntryAdded);
        Assert.Equal("Watched on 2026-10-31 · removed from the watchlist", entry.Detail);
    }

    [Fact]
    public async Task A_refused_viewing_is_not_logged()
    {
        var movie = AddTmdbMovie();

        await _diaryService.CreateAsync(Alice, Details(movie, "2030-01-01"), Ct);
        await _diaryService.CreateAsync(Alice, Details(movie) with { MovieId = Guid.NewGuid() }, Ct);

        Assert.Empty(_log.Entries);
    }

    [Fact]
    public async Task Changing_the_rating_of_a_viewing_is_logged_with_the_old_and_the_new_rating()
    {
        var movie = AddTmdbMovie();
        var created = await _diaryService.CreateAsync(Alice, Details(movie, rating: 6), Ct);
        _log.Entries.Clear();
        _clock.Advance(TimeSpan.FromHours(1));

        await _diaryService.UpdateAsync(Alice, created.Entry!.Id, Details(movie, rating: 9), Ct);

        var entry = Single(_log.Entries, ActivityType.DiaryEntryUpdated);
        Assert.Equal(("Alien", Start.AddHours(1), "Rating 3 stars → 4.5 stars"), (entry.MovieTitle, entry.OccurredAt, entry.Detail));
    }

    [Fact]
    public async Task Changing_several_things_of_a_viewing_names_all_of_them()
    {
        var movie = AddTmdbMovie();
        var created = await _diaryService.CreateAsync(Alice, Details(movie, rating: null), Ct);
        _log.Entries.Clear();

        await _diaryService.UpdateAsync(
            Alice, created.Entry!.Id, Details(movie, "2026-10-30", rating: 2, review: "Fine.", isRewatch: true, liked: true), Ct);

        var entry = Single(_log.Entries, ActivityType.DiaryEntryUpdated);
        Assert.Equal("Watched on 2026-10-30 · rated 1 star · review added · marked as rewatch · liked", entry.Detail);
    }

    [Fact]
    public async Task Saving_a_viewing_without_changes_is_not_logged()
    {
        var movie = AddTmdbMovie();
        var created = await _diaryService.CreateAsync(Alice, Details(movie), Ct);
        _log.Entries.Clear();

        await _diaryService.UpdateAsync(Alice, created.Entry!.Id, Details(movie), Ct);

        Assert.Empty(_log.Entries);
    }

    [Fact]
    public async Task Deleting_a_viewing_is_logged()
    {
        var movie = AddTmdbMovie();
        var created = await _diaryService.CreateAsync(Alice, Details(movie, "2026-10-31"), Ct);
        _log.Entries.Clear();

        await _diaryService.DeleteAsync(Alice, created.Entry!.Id, Ct);

        var entry = Single(_log.Entries, ActivityType.DiaryEntryDeleted);
        Assert.Equal((movie.Id, "Alien", "Watched on 2026-10-31"), (entry.MovieId, entry.MovieTitle, entry.Detail));
    }

    [Fact]
    public async Task Deleting_somebody_elses_viewing_is_not_logged()
    {
        var movie = AddTmdbMovie();
        var created = await _diaryService.CreateAsync(Bob, Details(movie), Ct);
        _log.Entries.Clear();

        Assert.False(await _diaryService.DeleteAsync(Alice, created.Entry!.Id, Ct));

        Assert.Empty(_log.Entries);
    }

    // --- Watchlist

    [Fact]
    public async Task Putting_a_movie_on_the_watchlist_is_logged_with_the_note()
    {
        var movie = AddTmdbMovie();

        await _watchlistService.PutAsync(Alice, movie.Id, "Director's cut", Ct);

        var entry = Single(_log.Entries, ActivityType.WatchlistItemAdded);
        Assert.Equal((Alice, movie.Id, "Alien", "Note: Director's cut"), (entry.UserId, entry.MovieId, entry.MovieTitle, entry.Detail));
    }

    [Fact]
    public async Task Putting_a_movie_on_the_watchlist_without_a_note_has_no_detail()
    {
        var movie = AddTmdbMovie();

        await _watchlistService.PutAsync(Alice, movie.Id, null, Ct);

        Assert.Null(Single(_log.Entries, ActivityType.WatchlistItemAdded).Detail);
    }

    [Fact]
    public async Task Changing_the_note_is_logged()
    {
        var movie = AddTmdbMovie();
        await _watchlistService.PutAsync(Alice, movie.Id, "first", Ct);
        _log.Entries.Clear();

        await _watchlistService.PutAsync(Alice, movie.Id, "second", Ct);

        Assert.Equal("Note: second", Single(_log.Entries, ActivityType.WatchlistNoteChanged).Detail);
    }

    [Fact]
    public async Task Removing_the_note_is_logged()
    {
        var movie = AddTmdbMovie();
        await _watchlistService.PutAsync(Alice, movie.Id, "first", Ct);
        _log.Entries.Clear();

        await _watchlistService.PutAsync(Alice, movie.Id, null, Ct);

        Assert.Equal("Note removed", Single(_log.Entries, ActivityType.WatchlistNoteChanged).Detail);
    }

    [Fact]
    public async Task Putting_the_same_note_again_is_not_logged()
    {
        var movie = AddTmdbMovie();
        await _watchlistService.PutAsync(Alice, movie.Id, "same", Ct);
        _log.Entries.Clear();

        await _watchlistService.PutAsync(Alice, movie.Id, "same", Ct);

        Assert.Empty(_log.Entries);
    }

    [Fact]
    public async Task Removing_a_movie_from_the_watchlist_is_logged()
    {
        var movie = AddTmdbMovie();
        await _watchlistService.PutAsync(Alice, movie.Id, null, Ct);
        _log.Entries.Clear();

        Assert.True(await _watchlistService.RemoveAsync(Alice, movie.Id, Ct));

        var entry = Single(_log.Entries, ActivityType.WatchlistItemRemoved);
        Assert.Equal((movie.Id, "Alien"), (entry.MovieId, entry.MovieTitle));
    }

    [Fact]
    public async Task Removing_a_movie_that_is_not_on_the_watchlist_is_not_logged()
    {
        var movie = AddTmdbMovie();

        Assert.False(await _watchlistService.RemoveAsync(Alice, movie.Id, Ct));

        Assert.Empty(_log.Entries);
    }

    // --- Movies

    [Fact]
    public async Task Adding_a_movie_manually_is_logged()
    {
        var details = new MovieDetails("Home Video", null, null, null, null, null);

        var result = await _movieService.CreateAsync(Alice, details, [], Ct);

        var entry = Single(_log.Entries, ActivityType.MovieAdded);
        Assert.Equal((result.Movie!.Id, "Home Video"), (entry.MovieId, entry.MovieTitle));
    }

    [Fact]
    public async Task Changing_a_manual_movie_is_logged_with_its_new_title()
    {
        var created = await _movieService.CreateAsync(Alice, new MovieDetails("Home Video", null, null, null, null, null), [], Ct);
        _log.Entries.Clear();

        await _movieService.UpdateAsync(Alice, created.Movie!.Id, new MovieDetails("Holiday Video", null, null, null, null, null), [], Ct);

        var entry = Single(_log.Entries, ActivityType.MovieUpdated);
        Assert.Equal((created.Movie.Id, "Holiday Video"), (entry.MovieId, entry.MovieTitle));
    }

    [Fact]
    public async Task Deleting_a_manual_movie_is_logged_without_a_reference_to_the_movie()
    {
        var created = await _movieService.CreateAsync(Alice, new MovieDetails("Home Video", null, null, null, null, null), [], Ct);
        _log.Entries.Clear();

        await _movieService.DeleteAsync(Alice, created.Movie!.Id, Ct);

        var entry = Single(_log.Entries, ActivityType.MovieDeleted);
        Assert.Equal((null, "Home Video"), (entry.MovieId, entry.MovieTitle));
    }

    [Fact]
    public async Task A_refused_movie_change_is_not_logged()
    {
        var tmdbMovie = AddTmdbMovie();

        var update = await _movieService.UpdateAsync(
            Alice, tmdbMovie.Id, new MovieDetails("Changed", null, null, null, null, null), [], Ct);
        var delete = await _movieService.DeleteAsync(Alice, tmdbMovie.Id, Ct);

        Assert.Equal(SaveMovieStatus.ReadOnly, update.Status);
        Assert.Equal(DeleteMovieResult.ReadOnly, delete);
        Assert.Empty(_log.Entries);
    }

    [Fact]
    public async Task Importing_a_movie_from_TMDB_is_logged_the_first_time_only()
    {
        _tmdb.Movies[348] = new TmdbMovie(348, new MovieDetails("Alien", "Alien", new DateOnly(1979, 5, 25), 117, null, null), []);

        var first = await _movieService.ImportTmdbMovieAsync(Alice, 348, Ct);
        var second = await _movieService.ImportTmdbMovieAsync(Bob, 348, Ct);

        Assert.Equal(ImportTmdbMovieStatus.Imported, first.Status);
        Assert.Equal(ImportTmdbMovieStatus.AlreadyImported, second.Status);
        var entry = Single(_log.Entries, ActivityType.MovieImported);
        Assert.Equal((Alice, first.Movie!.Id, "Alien"), (entry.UserId, entry.MovieId, entry.MovieTitle));
    }

    // --- Listing

    [Fact]
    public async Task List_returns_the_most_recent_actions_of_the_user_first_and_nobody_elses()
    {
        var movie = AddTmdbMovie();
        await _watchlistService.PutAsync(Alice, movie.Id, null, Ct);
        _clock.Advance(TimeSpan.FromMinutes(1));
        await _watchlistService.PutAsync(Bob, movie.Id, null, Ct);
        _clock.Advance(TimeSpan.FromMinutes(1));
        await _diaryService.CreateAsync(Alice, Details(movie), Ct);

        var result = await _service.ListAsync(Alice, page: 1, pageSize: 20, Ct);

        Assert.Equal(
            [ActivityType.DiaryEntryAdded, ActivityType.WatchlistItemAdded], result.Items.Select(entry => entry.Type));
        Assert.Equal(2, result.TotalCount);
    }

    [Fact]
    public async Task List_pages_through_the_log()
    {
        var movie = AddTmdbMovie();
        for (var day = 1; day <= 5; day++)
        {
            await _diaryService.CreateAsync(Alice, Details(movie, $"2026-10-{day:00}"), Ct);
            _clock.Advance(TimeSpan.FromMinutes(1));
        }

        var second = await _service.ListAsync(Alice, page: 2, pageSize: 2, Ct);

        Assert.Equal((2, 2, 5), (second.Page, second.PageSize, second.TotalCount));
        Assert.Equal(
            ["Watched on 2026-10-03 · 4 stars", "Watched on 2026-10-02 · 4 stars"], second.Items.Select(entry => entry.Detail));
    }
}
