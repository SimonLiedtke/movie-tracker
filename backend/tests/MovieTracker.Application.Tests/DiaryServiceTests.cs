using MovieTracker.Application.Diary;
using MovieTracker.Application.Tests.Fakes;
using MovieTracker.Domain.Diary;
using MovieTracker.Domain.Movies;

namespace MovieTracker.Application.Tests;

public sealed class DiaryServiceTests
{
    // "Now" is 3 November 2026, 12:00 UTC.
    private static readonly DateTimeOffset Start = new(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid Alice = Guid.NewGuid();
    private static readonly Guid Bob = Guid.NewGuid();

    private readonly FakeMovieRepository _movies = new();
    private readonly FakeDiaryRepository _diary;
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly TestClock _clock = new(Start);
    private readonly DiaryService _service;

    public DiaryServiceTests()
    {
        _diary = new FakeDiaryRepository(_movies);
        _service = new DiaryService(_diary, _movies, _unitOfWork, _clock);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private Movie AddTmdbMovie(int tmdbId = 348, string title = "Alien")
    {
        var movie = Movie.FromTmdb(tmdbId, new MovieDetails(title, null, new DateOnly(1979, 5, 25), 117, null, null), [], Start);
        _movies.Movies.Add(movie);
        return movie;
    }

    private Movie AddManualMovie(Guid owner, string title = "Home Video")
    {
        var movie = Movie.CreateManual(owner, new MovieDetails(title, null, null, null, null, null), [], Start);
        _movies.Movies.Add(movie);
        return movie;
    }

    private DiaryEntry AddEntry(Guid userId, Movie movie, string watchedOn = "2026-10-31", int? rating = 8)
    {
        var entry = new DiaryEntry(
            userId,
            Details(movie, watchedOn, rating),
            Start);
        _diary.Entries.Add(entry);
        return entry;
    }

    private static DiaryEntryDetails Details(Movie movie, string watchedOn = "2026-10-31", int? rating = 8, string? review = "Great") =>
        new(movie.Id, DateOnly.Parse(watchedOn), rating is { } halfStars ? new Rating(halfStars) : null, review, IsRewatch: false, Liked: true);

    // --- Logging a viewing

    [Fact]
    public async Task Create_saves_the_viewing_for_the_user()
    {
        var movie = AddTmdbMovie();

        var result = await _service.CreateAsync(Alice, Details(movie, "2026-10-31", rating: 9, review: "Still terrifying."), Ct);

        Assert.Equal(SaveDiaryEntryStatus.Saved, result.Status);
        var saved = Assert.Single(_diary.Entries);
        Assert.Equal((Alice, movie.Id, new DateOnly(2026, 10, 31), new Rating(9)), (saved.UserId, saved.MovieId, saved.WatchedOn, saved.Rating));
        Assert.Equal(("Still terrifying.", true, Start), (saved.Review, saved.Liked, saved.CreatedAt));
        Assert.Equal(1, _unitOfWork.SaveCount);

        // The answer is the saved entry with its movie.
        Assert.Equal(saved.Id, result.Entry!.Id);
        Assert.Equal("Alien", result.Entry.Movie.Title);
    }

    [Theory]
    [InlineData("2026-11-03")] // today
    [InlineData("2026-11-04")] // tomorrow: the client's date can be ahead of UTC
    public async Task Create_accepts_today_and_tomorrow(string watchedOn)
    {
        var movie = AddTmdbMovie();

        var result = await _service.CreateAsync(Alice, Details(movie, watchedOn), Ct);

        Assert.Equal(SaveDiaryEntryStatus.Saved, result.Status);
    }

    [Fact]
    public async Task Create_rejects_a_date_more_than_one_day_ahead()
    {
        var movie = AddTmdbMovie();

        var result = await _service.CreateAsync(Alice, Details(movie, "2026-11-05"), Ct);

        Assert.Equal(SaveDiaryEntryStatus.FutureDate, result.Status);
        Assert.Empty(_diary.Entries);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Create_follows_the_clock_when_judging_the_date()
    {
        var movie = AddTmdbMovie();
        _clock.Advance(TimeSpan.FromDays(2));

        var result = await _service.CreateAsync(Alice, Details(movie, "2026-11-05"), Ct);

        Assert.Equal(SaveDiaryEntryStatus.Saved, result.Status);
    }

    [Fact]
    public async Task Create_rejects_unknown_movies()
    {
        var unknown = new DiaryEntryDetails(Guid.NewGuid(), new DateOnly(2026, 10, 31), null, null, false, false);

        var result = await _service.CreateAsync(Alice, unknown, Ct);

        Assert.Equal(SaveDiaryEntryStatus.UnknownMovie, result.Status);
        Assert.Empty(_diary.Entries);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Create_rejects_other_users_manual_movies()
    {
        var movie = AddManualMovie(Bob);

        var result = await _service.CreateAsync(Alice, Details(movie), Ct);

        Assert.Equal(SaveDiaryEntryStatus.UnknownMovie, result.Status);
    }

    [Fact]
    public async Task Create_checks_the_date_before_the_movie()
    {
        var unknown = new DiaryEntryDetails(Guid.NewGuid(), new DateOnly(2030, 1, 1), null, null, false, false);

        var result = await _service.CreateAsync(Alice, unknown, Ct);

        Assert.Equal(SaveDiaryEntryStatus.FutureDate, result.Status);
    }

    // --- Changing a viewing

    [Fact]
    public async Task Update_changes_the_viewing()
    {
        var alien = AddTmdbMovie(348, "Alien");
        var aliens = AddTmdbMovie(679, "Aliens");
        var entry = AddEntry(Alice, alien);

        var result = await _service.UpdateAsync(Alice, entry.Id, Details(aliens, "2026-11-01", rating: 10, review: "Even better."), Ct);

        Assert.Equal(SaveDiaryEntryStatus.Saved, result.Status);
        Assert.Equal((aliens.Id, new DateOnly(2026, 11, 1), new Rating(10), "Even better."), (entry.MovieId, entry.WatchedOn, entry.Rating, entry.Review));
        Assert.Equal(1, _unitOfWork.SaveCount);
        Assert.Equal("Aliens", result.Entry!.Movie.Title);
    }

    [Fact]
    public async Task Update_does_not_find_other_users_entries()
    {
        var movie = AddTmdbMovie();
        var entry = AddEntry(Bob, movie, rating: 8);

        var result = await _service.UpdateAsync(Alice, entry.Id, Details(movie, rating: 1), Ct);

        Assert.Equal(SaveDiaryEntryStatus.NotFound, result.Status);
        Assert.Equal(new Rating(8), entry.Rating);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Update_looks_for_the_entry_before_it_checks_the_new_details()
    {
        var unknownMovieInTheFuture = new DiaryEntryDetails(Guid.NewGuid(), new DateOnly(2030, 1, 1), null, null, false, false);

        var result = await _service.UpdateAsync(Alice, Guid.NewGuid(), unknownMovieInTheFuture, Ct);

        Assert.Equal(SaveDiaryEntryStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task Update_rejects_a_future_date_and_leaves_the_entry_as_it_was()
    {
        var movie = AddTmdbMovie();
        var entry = AddEntry(Alice, movie, "2026-10-31");

        var result = await _service.UpdateAsync(Alice, entry.Id, Details(movie, "2026-11-05"), Ct);

        Assert.Equal(SaveDiaryEntryStatus.FutureDate, result.Status);
        Assert.Equal(new DateOnly(2026, 10, 31), entry.WatchedOn);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Update_rejects_a_movie_the_user_cannot_see_and_leaves_the_entry_as_it_was()
    {
        var movie = AddTmdbMovie();
        var entry = AddEntry(Alice, movie);
        var others = AddManualMovie(Bob);

        var result = await _service.UpdateAsync(Alice, entry.Id, Details(others), Ct);

        Assert.Equal(SaveDiaryEntryStatus.UnknownMovie, result.Status);
        Assert.Equal(movie.Id, entry.MovieId);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    // --- Deleting a viewing

    [Fact]
    public async Task Delete_removes_the_users_entry()
    {
        var entry = AddEntry(Alice, AddTmdbMovie());

        Assert.True(await _service.DeleteAsync(Alice, entry.Id, Ct));

        Assert.Empty(_diary.Entries);
    }

    [Fact]
    public async Task Delete_does_not_remove_other_users_entries()
    {
        var entry = AddEntry(Bob, AddTmdbMovie());

        Assert.False(await _service.DeleteAsync(Alice, entry.Id, Ct));

        Assert.Contains(entry, _diary.Entries);
    }

    // --- Reading

    [Fact]
    public async Task Get_returns_the_entry_only_to_its_owner()
    {
        var entry = AddEntry(Alice, AddTmdbMovie());

        Assert.Equal(entry.Id, (await _service.GetAsync(Alice, entry.Id, Ct))!.Id);
        Assert.Null(await _service.GetAsync(Bob, entry.Id, Ct));
    }

    [Fact]
    public async Task List_asks_for_the_users_entries_with_the_filter_and_paging()
    {
        var movie = AddTmdbMovie();
        AddEntry(Alice, movie, "2026-10-01");
        AddEntry(Alice, movie, "2026-10-15");
        AddEntry(Alice, movie, "2026-10-31");
        AddEntry(Bob, movie, "2026-10-20");
        var filter = new DiaryFilter(new DateOnly(2026, 10, 10), null, movie.Id);

        var result = await _service.ListAsync(Alice, filter, page: 1, pageSize: 1, Ct);

        Assert.Equal((Alice, filter, 1, 1), _diary.LastList);
        Assert.Equal(2, result.TotalCount);
        Assert.Equal(new DateOnly(2026, 10, 31), Assert.Single(result.Items).WatchedOn);
    }

    [Fact]
    public async Task ListForMovie_returns_nothing_if_the_user_cannot_see_the_movie()
    {
        var others = AddManualMovie(Bob);

        Assert.Null(await _service.ListForMovieAsync(Alice, others.Id, Ct));
        Assert.Null(await _service.ListForMovieAsync(Alice, Guid.NewGuid(), Ct));
    }

    [Fact]
    public async Task ListForMovie_returns_the_users_own_viewings_of_the_movie_newest_first()
    {
        var alien = AddTmdbMovie(348, "Alien");
        var aliens = AddTmdbMovie(679, "Aliens");
        AddEntry(Alice, alien, "2026-09-01");
        AddEntry(Alice, alien, "2026-10-01");
        AddEntry(Alice, aliens, "2026-10-15");
        AddEntry(Bob, alien, "2026-10-20");

        var entries = await _service.ListForMovieAsync(Alice, alien.Id, Ct);

        Assert.Equal(
            [new DateOnly(2026, 10, 1), new DateOnly(2026, 9, 1)],
            entries!.Select(entry => entry.WatchedOn));
    }
}
