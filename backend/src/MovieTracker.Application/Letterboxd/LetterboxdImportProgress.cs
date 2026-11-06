namespace MovieTracker.Application.Letterboxd;

/// <summary>
/// How far an import has come. The caller creates it and hands it to <see cref="LetterboxdImporter"/>, which fills it from
/// several threads at once; whoever shows the progress reads it at any time. (Reading instead of reporting snapshots
/// means nothing can arrive out of order.)
/// </summary>
public sealed class LetterboxdImportProgress
{
    private int _filmsTotal;
    private int _filmsProcessed;

    /// <summary>The number of different films in the diary and the watchlist. 0 until the import has started.</summary>
    public int FilmsTotal => Volatile.Read(ref _filmsTotal);

    /// <summary>How many of the films have been looked up so far.</summary>
    public int FilmsProcessed => Volatile.Read(ref _filmsProcessed);

    /// <summary>Called by the importer once it knows how many films there are.</summary>
    public void Start(int filmsTotal) => Volatile.Write(ref _filmsTotal, filmsTotal);

    /// <summary>Called by the importer after each film, possibly from several threads at once.</summary>
    public void FilmProcessed() => Interlocked.Increment(ref _filmsProcessed);
}
