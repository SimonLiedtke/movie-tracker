using System.Globalization;
using MovieTracker.Domain.Diary;

namespace MovieTracker.Application.Activity;

/// <summary>The short texts that say what exactly happened, for <see cref="Domain.Activity.ActivityLogEntry.Detail"/>.</summary>
internal static class ActivityDetails
{
    public static string Viewing(DiaryEntryDetails details, bool removedFromWatchlist)
    {
        var parts = new List<string> { $"Watched on {details.WatchedOn:yyyy-MM-dd}" };
        if (details.Rating is { } rating)
        {
            parts.Add(Stars(rating));
        }

        if (details.IsRewatch)
        {
            parts.Add("rewatch");
        }

        if (details.Liked)
        {
            parts.Add("liked");
        }

        if (removedFromWatchlist)
        {
            parts.Add("removed from the watchlist");
        }

        return string.Join(" · ", parts);
    }

    /// <summary>What differs between the two versions of a diary entry. Null if nothing does.</summary>
    public static string? DiaryEntryChanges(DiaryEntryDetails before, DiaryEntryDetails after)
    {
        var changes = new List<string>();

        if (before.MovieId != after.MovieId)
        {
            changes.Add("movie changed");
        }

        if (before.WatchedOn != after.WatchedOn)
        {
            changes.Add($"watched on {after.WatchedOn:yyyy-MM-dd}");
        }

        if (before.Rating != after.Rating)
        {
            changes.Add(after.Rating is { } rating
                ? before.Rating is { } previous ? $"rating {Stars(previous)} → {Stars(rating)}" : $"rated {Stars(rating)}"
                : "rating removed");
        }

        if (before.Review != after.Review)
        {
            changes.Add(string.IsNullOrWhiteSpace(after.Review)
                ? "review removed"
                : string.IsNullOrWhiteSpace(before.Review) ? "review added" : "review changed");
        }

        if (before.IsRewatch != after.IsRewatch)
        {
            changes.Add(after.IsRewatch ? "marked as rewatch" : "no longer a rewatch");
        }

        if (before.Liked != after.Liked)
        {
            changes.Add(after.Liked ? "liked" : "unliked");
        }

        return changes.Count == 0 ? null : Capitalize(string.Join(" · ", changes));
    }

    public static string? Note(string? note) => string.IsNullOrWhiteSpace(note) ? null : $"Note: {note}";

    public static string NoteChange(string? note) => Note(note) ?? "Note removed";

    private static string Stars(Rating rating)
    {
        var stars = rating.Stars.ToString("0.#", CultureInfo.InvariantCulture);
        return rating.HalfStars == 2 ? $"{stars} star" : $"{stars} stars";
    }

    private static string Capitalize(string text) => char.ToUpperInvariant(text[0]) + text[1..];
}
