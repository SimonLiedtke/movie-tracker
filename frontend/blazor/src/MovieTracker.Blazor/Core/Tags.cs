using System.Text.RegularExpressions;

namespace MovieTracker.Blazor.Core;

public static partial class Tags
{
    /// <summary>The server accepts at most this many different tags per entry, each at most <see cref="MaxTagLength"/> long.</summary>
    public const int MaxTags = 20;

    public const int MaxTagLength = 50;

    /// <summary>
    /// Splits the text of a tags field ("horror, date night") into tags. Blank ones are left out; the server
    /// stores tags trimmed, in lower case and without duplicates.
    /// </summary>
    public static IReadOnlyList<string> Parse(string text) =>
        text.Split(',').Select(tag => tag.Trim()).Where(tag => tag != "").ToList();

    /// <summary>The tags as the text of a tags field.</summary>
    public static string Format(IEnumerable<string> tags) => string.Join(", ", tags);

    /// <summary>Why the text of a tags field is refused, or null if it is fine. Mirrors the server's rules.</summary>
    public static string? Error(string text)
    {
        var tags = Parse(text);
        if (tags.Any(tag => tag.Length > MaxTagLength))
        {
            return $"A tag can have at most {MaxTagLength} characters.";
        }

        var different = tags.Select(tag => Whitespace().Replace(tag.ToLowerInvariant(), " ")).ToHashSet();
        return different.Count > MaxTags ? $"At most {MaxTags} different tags are allowed." : null;
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
