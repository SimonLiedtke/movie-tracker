namespace MovieTracker.Domain.Tags;

/// <summary>
/// The rules for the tags of diary entries and watchlist items. A tag is a short, free text; its case and the spaces
/// around and inside it do not matter, so "Sci-Fi " and "sci-fi" are the same tag.
/// </summary>
public static class TagList
{
    public const int MaxTags = 20;
    public const int MaxTagLength = 50;

    /// <summary>
    /// The tags in their stored form: trimmed, with runs of spaces shortened to one, lower case, without blanks and
    /// duplicates, in alphabetical order, so that the same tags are always equal.
    /// </summary>
    public static string[] Normalize(IEnumerable<string>? tags) =>
        tags is null
            ? []
            : tags
                .Select(NormalizeTag)
                .Where(tag => tag.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray();

    /// <summary>The tags of both lists. Nothing is dropped, so the result can be longer than <see cref="MaxTags"/>.</summary>
    public static string[] Merge(IEnumerable<string>? first, IEnumerable<string>? second) =>
        Normalize((first ?? []).Concat(second ?? []));

    /// <summary>One tag in its stored form; empty if it is blank.</summary>
    public static string NormalizeTag(string? tag) =>
        tag is null
            ? string.Empty
            : string.Join(' ', tag.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                .ToLowerInvariant();
}
