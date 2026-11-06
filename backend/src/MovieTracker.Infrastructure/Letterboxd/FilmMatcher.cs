using System.Globalization;
using System.Text;
using MovieTracker.Application.Tmdb;

namespace MovieTracker.Infrastructure.Letterboxd;

/// <summary>
/// Decides whether a movie is the film a Letterboxd export means. The export only has the name and the release year,
/// so both must fit; guessing a wrong movie is worse than not finding one.
/// </summary>
public static class FilmMatcher
{
    /// <summary>
    /// Letterboxd and TMDB sometimes disagree about the release year by one (festival premiere versus theatrical release).
    /// </summary>
    private const int MaxYearDifference = 1;

    /// <summary>
    /// How well a movie fits the film: 0 is best (same year, or the year is unknown), 1 is one year off,
    /// null means it is not the film (different title, or the years are too far apart).
    /// </summary>
    public static int? Match(LetterboxdFilm film, string title, string? originalTitle, DateOnly? releaseDate)
    {
        ArgumentNullException.ThrowIfNull(film);

        var name = Normalize(film.Name);
        if (name.Length == 0 || (Normalize(title) != name && Normalize(originalTitle) != name))
        {
            return null;
        }

        if (film.Year is not { } year)
        {
            return 0;
        }

        if (releaseDate is null)
        {
            return null;
        }

        var difference = Math.Abs(releaseDate.Value.Year - year);
        return difference <= MaxYearDifference ? difference : null;
    }

    /// <summary>The best of TMDB's search results, or null if none is the film. Among equally good results, TMDB's order decides.</summary>
    public static TmdbSearchResult? FindBest(LetterboxdFilm film, IEnumerable<TmdbSearchResult> results)
    {
        TmdbSearchResult? best = null;
        var bestRank = int.MaxValue;

        foreach (var result in results)
        {
            // Strictly better only, so TMDB's order decides between equally good results.
            if (Match(film, result.Title, result.OriginalTitle, result.ReleaseDate) is { } rank && rank < bestRank)
            {
                best = result;
                bestRank = rank;
            }
        }

        return best;
    }

    /// <summary>
    /// Ignores case, accents and everything that is not a letter or digit, so that "Amélie", "AMELIE" and
    /// "WALL·E" / "Wall-E" count as the same title.
    /// </summary>
    internal static string Normalize(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return string.Empty;
        }

        var normalized = new StringBuilder(title.Length);
        foreach (var character in title.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(character))
            {
                normalized.Append(char.ToLowerInvariant(character));
            }
        }

        return normalized.ToString();
    }
}
