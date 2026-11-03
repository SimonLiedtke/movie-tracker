using MovieTracker.Domain.Movies;

namespace MovieTracker.Domain.Tests;

public sealed class GenreTests
{
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Rejects_a_blank_name(string name)
    {
        Assert.Throws<ArgumentException>(() => new Genre(1, name));
    }
}
