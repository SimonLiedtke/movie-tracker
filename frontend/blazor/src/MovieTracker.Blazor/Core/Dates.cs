using System.Globalization;

namespace MovieTracker.Blazor.Core;

/// <summary>Dates as the app shows them: English, whatever the browser's language.</summary>
public static class Dates
{
    private static readonly CultureInfo English = CultureInfo.InvariantCulture;

    /// <summary>Today's date in the browser's time zone.</summary>
    public static DateOnly Today() => DateOnly.FromDateTime(DateTime.Now);

    /// <summary>"YYYY-MM-DD", the format of date fields and query parameters.</summary>
    public static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd", English);

    public static string Iso(DateOnly? date) => date is { } value ? Iso(value) : "";

    /// <summary>Parses a "YYYY-MM-DD" date; null if the text is not one.</summary>
    public static DateOnly? ParseIso(string? text) =>
        DateOnly.TryParseExact(text, "yyyy-MM-dd", English, DateTimeStyles.None, out var date) ? date : null;

    /// <summary>The year of a date, or an empty string.</summary>
    public static string YearOf(DateOnly? date) => date is { } value ? value.Year.ToString("0000", English) : "";

    /// <summary>The "YYYY-MM" month of a date.</summary>
    public static string MonthOf(DateOnly date) => date.ToString("yyyy-MM", English);

    /// <summary>The month of a date, e.g. "November 2026".</summary>
    public static string FormatMonth(DateOnly date) => date.ToString("MMMM yyyy", English);

    /// <summary>The day within its month, e.g. "Sat 7".</summary>
    public static string FormatDay(DateOnly date) => date.ToString("ddd d", English);

    /// <summary>A full, human-friendly date, e.g. "November 7, 2026".</summary>
    public static string FormatDate(DateOnly date) => date.ToString("MMMM d, yyyy", English);

    /// <summary>A "YYYY-MM-DD" date as <see cref="FormatDate(DateOnly)"/>; text that is no date stays as it is.</summary>
    public static string FormatDate(string isoDate) => ParseIso(isoDate) is { } date ? FormatDate(date) : isoDate;

    /// <summary>A point in time in the browser's time zone, e.g. "Nov 7, 2026, 9:30 PM".</summary>
    public static string FormatDateTime(DateTimeOffset timestamp) =>
        timestamp.ToLocalTime().ToString("MMM d, yyyy, h:mm tt", English);
}
