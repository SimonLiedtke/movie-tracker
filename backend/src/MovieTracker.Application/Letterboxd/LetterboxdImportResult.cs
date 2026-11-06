namespace MovieTracker.Application.Letterboxd;

/// <param name="DiaryEntriesSkipped">Viewings that were in the diary already (same movie, same day).</param>
/// <param name="WatchlistItemsSkipped">Films that were on the watchlist already.</param>
/// <param name="ManualFilms">Films that TMDB does not know (or there is no TMDB access): added as manual movies.</param>
/// <param name="FailedFilms">Films that could not be looked up because TMDB failed: not imported, importing again retries them.</param>
/// <param name="Warnings">Rows of the export that were skipped, and why.</param>
public sealed record LetterboxdImportResult(
    int DiaryEntriesImported,
    int DiaryEntriesSkipped,
    int WatchlistItemsImported,
    int WatchlistItemsSkipped,
    IReadOnlyList<LetterboxdFilm> ManualFilms,
    IReadOnlyList<LetterboxdFilm> FailedFilms,
    IReadOnlyList<string> Warnings);
