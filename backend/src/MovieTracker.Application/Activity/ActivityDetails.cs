using System.Globalization;
using MovieTracker.Domain.Diary;
using MovieTracker.Domain.Tags;

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

        if (TagList.Normalize(details.Tags) is { Length: > 0 } tags)
        {
            parts.Add($"tagged {string.Join(", ", tags)}");
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

        if (TagChange(before.Tags, after.Tags) is { } tagChange)
        {
            changes.Add(tagChange);
        }

        return changes.Count == 0 ? null : Capitalize(string.Join(" · ", changes));
    }

    /// <summary>What differs between the tags before and after, e.g. "tags added: a, b · tags removed: c". Null if nothing does.</summary>
    public static string? TagChange(IEnumerable<string>? before, IEnumerable<string>? after)
    {
        var previous = TagList.Normalize(before);
        var current = TagList.Normalize(after);
        var added = current.Except(previous).ToList();
        var removed = previous.Except(current).ToList();

        var parts = new List<string>();
        if (added.Count > 0)
        {
            parts.Add($"tags added: {string.Join(", ", added)}");
        }

        if (removed.Count > 0)
        {
            parts.Add($"tags removed: {string.Join(", ", removed)}");
        }

        return parts.Count == 0 ? null : string.Join(" · ", parts);
    }

    /// <summary>The same as <see cref="TagChange"/>, as a sentence of its own.</summary>
    public static string? WatchlistTagChange(IEnumerable<string>? before, IEnumerable<string>? after) =>
        TagChange(before, after) is { } change ? Capitalize(change) : null;

    /// <summary>What a new watchlist item has: its note and tags. Null if it has neither.</summary>
    public static string? WatchlistItem(string? note, IEnumerable<string>? tags)
    {
        var parts = new List<string>();
        if (Note(note) is { } noteText)
        {
            parts.Add(noteText);
        }

        if (TagList.Normalize(tags) is { Length: > 0 } normalized)
        {
            parts.Add($"tagged {string.Join(", ", normalized)}");
        }

        return parts.Count == 0 ? null : string.Join(" · ", parts);
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
