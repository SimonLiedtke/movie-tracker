using System.Globalization;

namespace MovieTracker.Blazor.Core;

public static class RatingFormat
{
    /// <summary>Formats a rating in half stars (1-10) as stars, e.g. 7 -> "★★★½".</summary>
    public static string Format(int? halfStars)
    {
        if (halfStars is not > 0)
        {
            return "";
        }

        return new string('★', halfStars.Value / 2) + (halfStars.Value % 2 == 1 ? "½" : "");
    }

    /// <summary>"3.5 of 5 stars"</summary>
    public static string Describe(int? halfStars) =>
        $"{((halfStars ?? 0) / 2.0).ToString(CultureInfo.InvariantCulture)} of 5 stars";

    /// <summary>Choices for a rating select: 0.5 to 5 stars.</summary>
    public static IReadOnlyList<RatingOption> Options { get; } = Enumerable.Range(1, 10)
        .Select(halfStars => new RatingOption(
            halfStars, $"{Format(halfStars)} ({(halfStars / 2.0).ToString(CultureInfo.InvariantCulture)})"))
        .ToList();
}

public sealed record RatingOption(int Value, string Label);
