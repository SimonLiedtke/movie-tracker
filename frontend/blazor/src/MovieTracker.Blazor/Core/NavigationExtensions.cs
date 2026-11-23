using Microsoft.AspNetCore.Components;

namespace MovieTracker.Blazor.Core;

/// <summary>The URL's query parameters drive the pages (filters, page): changing them is navigating.</summary>
public static class NavigationExtensions
{
    /// <summary>
    /// The current page's URL with some query parameters changed: those with a value are set, those without
    /// (<c>null</c>, "" or an empty collection) are removed, all others stay.
    /// </summary>
    public static string UrlWith(this NavigationManager navigation, params (string Key, object? Value)[] changes) =>
        navigation.LinkTo(null, changes);

    /// <summary>
    /// A link to <paramref name="path"/> (the current page if null) that carries the current query parameters,
    /// changed as in <see cref="UrlWith"/>.
    /// </summary>
    public static string LinkTo(this NavigationManager navigation, string? path, params (string Key, object? Value)[] changes)
    {
        var current = new Uri(navigation.Uri);
        return (path ?? current.AbsolutePath) + QueryString.Merge(current.Query, changes);
    }

    /// <summary>Goes to the current page with some query parameters changed (see <see cref="UrlWith"/>).</summary>
    public static void NavigateWith(this NavigationManager navigation, params (string Key, object? Value)[] changes) =>
        navigation.NavigateTo(navigation.UrlWith(changes));

    /// <summary>Like <see cref="NavigateWith"/>, but replaces the current history entry (for what is typed in a field).</summary>
    public static void ReplaceWith(this NavigationManager navigation, params (string Key, object? Value)[] changes) =>
        navigation.NavigateTo(navigation.UrlWith(changes), replace: true);

    /// <summary>The page, from page 1 up: goes to the same page without any query parameters.</summary>
    public static void ClearQuery(this NavigationManager navigation) =>
        navigation.NavigateTo(new Uri(navigation.Uri).AbsolutePath);
}
