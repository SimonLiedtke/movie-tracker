using MovieTracker.Application.Tests.Fakes;
using MovieTracker.Application.Watchlist;
using MovieTracker.Domain.Movies;
using MovieTracker.Domain.Watchlist;

namespace MovieTracker.Application.Tests;

public sealed class WatchlistServiceTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid Alice = Guid.NewGuid();
    private static readonly Guid Bob = Guid.NewGuid();

    private readonly FakeMovieRepository _movies = new();
    private readonly FakeWatchlistRepository _watchlist;
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly TestClock _clock = new(Start);
    private readonly WatchlistService _service;

    public WatchlistServiceTests()
    {
        _watchlist = new FakeWatchlistRepository(_movies);
        _service = new WatchlistService(_watchlist, _movies, _unitOfWork, _clock);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private Movie AddTmdbMovie(int tmdbId = 348, string title = "Alien")
    {
        var movie = Movie.FromTmdb(tmdbId, new MovieDetails(title, null, new DateOnly(1979, 5, 25), 117, null, null), [], Start);
        _movies.Movies.Add(movie);
        return movie;
    }

    private Movie AddManualMovie(Guid owner)
    {
        var movie = Movie.CreateManual(owner, new MovieDetails("Home Video", null, null, null, null, null), [], Start);
        _movies.Movies.Add(movie);
        return movie;
    }

    // --- Putting a movie on the watchlist

    [Fact]
    public async Task Put_adds_the_movie_with_the_note_and_the_current_time()
    {
        var movie = AddTmdbMovie();

        var result = await _service.PutAsync(Alice, movie.Id, "Director's cut", Ct);

        Assert.Equal(PutWatchlistItemStatus.Added, result.Status);
        var item = Assert.Single(_watchlist.Items);
        Assert.Equal((Alice, movie.Id, "Director's cut", Start), (item.UserId, item.MovieId, item.Note, item.AddedAt));
        Assert.Equal(1, _unitOfWork.SaveCount);

        // The answer is the saved item with its movie.
        Assert.Equal(("Alien", "Director's cut", Start), (result.Item!.Movie.Title, result.Item.Note, result.Item.AddedAt));
    }

    [Fact]
    public async Task Put_without_a_note_adds_the_movie_without_one()
    {
        var movie = AddTmdbMovie();

        await _service.PutAsync(Alice, movie.Id, null, Ct);

        Assert.Null(Assert.Single(_watchlist.Items).Note);
    }

    [Fact]
    public async Task Put_again_replaces_the_note_and_keeps_the_date_added()
    {
        var movie = AddTmdbMovie();
        await _service.PutAsync(Alice, movie.Id, "first", Ct);
        _clock.Advance(TimeSpan.FromDays(1));

        var result = await _service.PutAsync(Alice, movie.Id, "second", Ct);

        Assert.Equal(PutWatchlistItemStatus.Updated, result.Status);
        var item = Assert.Single(_watchlist.Items);
        Assert.Equal(("second", Start), (item.Note, item.AddedAt));
        Assert.Equal(2, _unitOfWork.SaveCount);
        Assert.Equal("second", result.Item!.Note);
    }

    [Fact]
    public async Task Put_again_without_a_note_removes_the_note()
    {
        var movie = AddTmdbMovie();
        await _service.PutAsync(Alice, movie.Id, "first", Ct);

        await _service.PutAsync(Alice, movie.Id, null, Ct);

        Assert.Null(Assert.Single(_watchlist.Items).Note);
    }

    [Fact]
    public async Task Put_does_not_find_unknown_movies()
    {
        var result = await _service.PutAsync(Alice, Guid.NewGuid(), "?", Ct);

        Assert.Equal(PutWatchlistItemStatus.NotFound, result.Status);
        Assert.Empty(_watchlist.Items);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Put_does_not_find_other_users_manual_movies()
    {
        var movie = AddManualMovie(Bob);

        var result = await _service.PutAsync(Alice, movie.Id, null, Ct);

        Assert.Equal(PutWatchlistItemStatus.NotFound, result.Status);
        Assert.Empty(_watchlist.Items);
    }

    [Fact]
    public async Task Every_user_has_their_own_watchlist()
    {
        var movie = AddTmdbMovie();
        await _service.PutAsync(Bob, movie.Id, "Bob's note", Ct);

        var result = await _service.PutAsync(Alice, movie.Id, "Alice's note", Ct);

        Assert.Equal(PutWatchlistItemStatus.Added, result.Status);
        Assert.Equal(
            [(Bob, "Bob's note"), (Alice, "Alice's note")],
            _watchlist.Items.Select(item => (item.UserId, item.Note)));
    }

    // --- Removing a movie from the watchlist

    [Fact]
    public async Task Remove_takes_the_movie_off_the_users_watchlist()
    {
        var movie = AddTmdbMovie();
        await _service.PutAsync(Alice, movie.Id, null, Ct);

        Assert.True(await _service.RemoveAsync(Alice, movie.Id, Ct));

        Assert.Empty(_watchlist.Items);
    }

    [Fact]
    public async Task Remove_does_not_touch_other_users_watchlists()
    {
        var movie = AddTmdbMovie();
        await _service.PutAsync(Bob, movie.Id, null, Ct);

        Assert.False(await _service.RemoveAsync(Alice, movie.Id, Ct));

        Assert.Single(_watchlist.Items);
    }

    // --- Listing

    [Fact]
    public async Task List_returns_the_users_own_items_most_recently_added_first()
    {
        var alien = AddTmdbMovie(348, "Alien");
        var aliens = AddTmdbMovie(679, "Aliens");
        await _service.PutAsync(Alice, alien.Id, null, Ct);
        _clock.Advance(TimeSpan.FromMinutes(1));
        await _service.PutAsync(Alice, aliens.Id, null, Ct);
        await _service.PutAsync(Bob, alien.Id, null, Ct);

        var result = await _service.ListAsync(Alice, WatchlistFilter.None, page: 1, pageSize: 10, Ct);

        Assert.Equal((Alice, WatchlistFilter.None, 1, 10), _watchlist.LastList);
        Assert.Equal(["Aliens", "Alien"], result.Items.Select(item => item.Movie.Title));
        Assert.Equal(2, result.TotalCount);
    }

    [Fact]
    public async Task List_passes_the_filter_on()
    {
        var filter = new WatchlistFilter([27, 878], GenreMatch.All);

        await _service.ListAsync(Alice, filter, page: 2, pageSize: 5, Ct);

        Assert.Equal((Alice, filter, 2, 5), _watchlist.LastList);
    }
}
