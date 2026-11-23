using System.Text.RegularExpressions;
using AngleSharp.Dom;

namespace MovieTracker.Blazor.Tests.Support;

public static partial class ElementExtensions
{
    /// <summary>The text as a browser shows it: runs of white space are one space.</summary>
    public static string Text(this IElement element) => WhiteSpace().Replace(element.TextContent, " ").Trim();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhiteSpace();
}
