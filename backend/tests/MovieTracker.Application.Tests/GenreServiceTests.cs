using MovieTracker.Application.Genres;
using MovieTracker.Application.Tests.Fakes;

namespace MovieTracker.Application.Tests;

public sealed class GenreServiceTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Lists_the_genres_of_the_repository_ordered_by_name()
    {
        var service = new GenreService(new FakeGenreRepository());

        var genres = await service.ListAsync(Ct);

        Assert.Equal(["Action", "Drama", "Horror", "Science Fiction"], genres.Select(genre => genre.Name));
    }
}
