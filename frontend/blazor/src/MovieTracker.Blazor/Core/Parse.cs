using System.Globalization;

namespace MovieTracker.Blazor.Core;

/// <summary>Reads numbers from query parameters and form fields: what is not a number is left out.</summary>
public static class Parse
{
    /// <summary>An integer, or null for anything else (e.g. from a hand-edited URL) and for 0.</summary>
    public static int? Number(string? value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) && number != 0 ? number : null;

    /// <summary>A whole number of minutes; anything else is left out.</summary>
    public static int? Minutes(string? value) => Number(value) is > 0 and var minutes ? minutes : null;

    /// <summary>The page number: 1 for anything that is not a page.</summary>
    public static int Page(string? value) => Number(value) is { } page ? page : 1;

    /// <summary>"" for no text.</summary>
    public static string? NullIfEmpty(string? value) => string.IsNullOrEmpty(value) ? null : value;
}
