namespace MovieTracker.Infrastructure.Letterboxd;

/// <summary>
/// A film as Letterboxd names it. The export has no TMDB IDs, so name and year are all there is to find the film again.
/// </summary>
/// <param name="Year">Null if Letterboxd does not know the release year.</param>
public sealed record LetterboxdFilm(string Name, int? Year);

/// <param name="Rating">In half stars (1 to 10), like <c>MovieTracker.Domain.Diary.Rating</c>.</param>
public sealed record LetterboxdDiaryEntry(
    LetterboxdFilm Film,
    DateOnly WatchedOn,
    int? Rating,
    bool IsRewatch,
    string? Review,
    bool Liked);

public sealed record LetterboxdWatchlistItem(LetterboxdFilm Film, DateOnly AddedOn);

/// <summary>The parts of a Letterboxd data export that the movie tracker can use.</summary>
/// <param name="Warnings">Rows that were skipped, and why. Shortened if there are many.</param>
public sealed record LetterboxdExport(
    IReadOnlyList<LetterboxdDiaryEntry> Diary,
    IReadOnlyList<LetterboxdWatchlistItem> Watchlist,
    IReadOnlyList<string> Warnings);

/// <summary>The uploaded file is not a Letterboxd export. The message can be shown to the user.</summary>
public sealed class InvalidLetterboxdExportException(string message, Exception? innerException = null)
    : Exception(message, innerException);
