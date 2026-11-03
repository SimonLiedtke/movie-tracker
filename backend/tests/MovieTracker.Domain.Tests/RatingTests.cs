using MovieTracker.Domain.Diary;

namespace MovieTracker.Domain.Tests;

public sealed class RatingTests
{
    [Theory]
    [InlineData(1, 0.5)]
    [InlineData(7, 3.5)]
    [InlineData(10, 5.0)]
    public void Converts_half_stars_to_stars(int halfStars, decimal expectedStars)
    {
        var rating = new Rating(halfStars);

        Assert.Equal(halfStars, rating.HalfStars);
        Assert.Equal(expectedStars, rating.Stars);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(11)]
    public void Rejects_values_outside_the_range(int halfStars)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Rating(halfStars));
    }

    [Fact]
    public void Has_value_equality()
    {
        Assert.Equal(new Rating(6), new Rating(6));
    }
}
