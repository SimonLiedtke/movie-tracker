namespace MovieTracker.Domain.Diary;

/// <summary>
/// A rating in half stars: 1 = 0.5 stars, 10 = 5 stars.
/// </summary>
public readonly record struct Rating
{
    public const int MinHalfStars = 1;
    public const int MaxHalfStars = 10;

    public Rating(int halfStars)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(halfStars, MinHalfStars);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(halfStars, MaxHalfStars);

        HalfStars = halfStars;
    }

    public int HalfStars { get; }

    public decimal Stars => HalfStars / 2m;
}
