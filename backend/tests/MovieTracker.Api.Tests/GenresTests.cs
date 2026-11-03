using MovieTracker.Api.Contracts;
using MovieTracker.Api.Tests.Infrastructure;

namespace MovieTracker.Api.Tests;

public sealed class GenresTests(ApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task Lists_the_seeded_genres_by_name()
    {
        var genres = await GetAsync<List<GenreResponse>>("/api/genres");

        Assert.Equal(19, genres.Count);
        Assert.Equal(genres.OrderBy(genre => genre.Name, StringComparer.Ordinal), genres);
        Assert.Contains(new GenreResponse(878, "Science Fiction"), genres);
    }
}
