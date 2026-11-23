using System.Collections;
using System.Globalization;

namespace MovieTracker.Blazor.Core.Api;

/// <summary>Builds query strings and changes the query of a URL.</summary>
public static class QueryString
{
    /// <summary>
    /// Builds a query string ("?a=1&amp;b=2", or "" if there is nothing to send), leaving out empty values.
    /// A collection repeats the parameter: <c>genreId=1&amp;genreId=2</c>.
    /// </summary>
    public static string Build(params (string Key, object? Value)[] parameters) =>
        Build((IEnumerable<(string Key, object? Value)>)parameters);

    public static string Build(IEnumerable<(string Key, object? Value)> parameters)
    {
        var pairs = new List<string>();
        foreach (var (key, value) in parameters)
        {
            foreach (var single in Values(value))
            {
                pairs.Add($"{Uri.EscapeDataString(key)}={Uri.EscapeDataString(single)}");
            }
        }

        return pairs.Count == 0 ? "" : "?" + string.Join('&', pairs);
    }

    /// <summary>
    /// The query with some parameters changed: those with a value are set, those without (<c>null</c>, "" or an
    /// empty collection) are removed, all others stay. A collection sets a parameter that is repeated.
    /// </summary>
    /// <param name="query">The current query, with or without the leading "?".</param>
    public static string Merge(string query, IEnumerable<(string Key, object? Value)> changes)
    {
        var pairs = Parse(query);
        foreach (var (key, value) in changes)
        {
            pairs.RemoveAll(pair => pair.Key == key);
            foreach (var single in Values(value))
            {
                pairs.Add(new KeyValuePair<string, string>(key, single));
            }
        }

        return Build(pairs.Select(pair => (pair.Key, (object?)pair.Value)));
    }

    /// <summary>The parameters of a query string, in order. A parameter that is given more than once appears more than once.</summary>
    public static List<KeyValuePair<string, string>> Parse(string query)
    {
        var pairs = new List<KeyValuePair<string, string>>();
        foreach (var part in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = part.IndexOf('=');
            var key = separator < 0 ? part : part[..separator];
            var value = separator < 0 ? "" : part[(separator + 1)..];
            pairs.Add(new KeyValuePair<string, string>(Decode(key), Decode(value)));
        }

        return pairs;
    }

    private static string Decode(string text) => Uri.UnescapeDataString(text.Replace('+', ' '));

    private static IEnumerable<string> Values(object? value)
    {
        var singles = value is IEnumerable and not string ? ((IEnumerable)value).Cast<object?>() : [value];
        foreach (var single in singles)
        {
            var text = single switch
            {
                null => null,
                bool flag => flag ? "true" : "false",
                IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
                _ => single.ToString(),
            };
            if (!string.IsNullOrEmpty(text))
            {
                yield return text;
            }
        }
    }
}
