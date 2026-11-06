using System.Globalization;
using System.IO.Compression;
using System.Text;

namespace MovieTracker.Infrastructure.Letterboxd;

/// <summary>
/// Reads the ZIP archive from Letterboxd (Settings -> Data -> Export Your Data). Only these files are used,
/// everything else in the archive (profile, lists, comments, ...) is never opened:
/// <list type="bullet">
/// <item><c>diary.csv</c>: the viewings (date, rating, rewatch).</item>
/// <item><c>reviews.csv</c>: the review texts, matched to the viewings by their Letterboxd URI.</item>
/// <item><c>likes/films.csv</c>: the liked films. Letterboxd likes films, not viewings, so all viewings of a liked film are liked.</item>
/// <item><c>watchlist.csv</c>: the films to watch later.</item>
/// </list>
/// </summary>
public static class LetterboxdExportReader
{
    /// <summary>The size of an empty ZIP archive: just the end-of-central-directory record.</summary>
    private const int MinZipBytes = 22;

    private const int MaxFileBytes = 20 * 1024 * 1024;

    /// <summary>
    /// Per file. Every film is looked up on TMDB, so a file with millions of rows would keep the import worker
    /// (shared by all users) busy for days. Even heavy Letterboxd users stay far below.
    /// </summary>
    private const int MaxRows = 20_000;
    private const int MaxWarnings = 20;
    private const string DateFormat = "yyyy-MM-dd";

    /// <exception cref="InvalidLetterboxdExportException">The stream is not a ZIP archive with a Letterboxd export.</exception>
    public static LetterboxdExport Read(Stream zip)
    {
        ArgumentNullException.ThrowIfNull(zip);

        // ZipArchive fails with an ArgumentOutOfRangeException instead of an InvalidDataException for tiny files.
        if (zip.CanSeek && zip.Length < MinZipBytes)
        {
            throw new InvalidLetterboxdExportException("The file is not a valid ZIP archive.");
        }

        try
        {
            using var archive = new ZipArchive(zip, ZipArchiveMode.Read, leaveOpen: true);
            return ReadArchive(archive);
        }
        catch (Exception exception) when (exception is InvalidDataException or NotSupportedException)
        {
            // NotSupportedException: e.g. an encrypted archive.
            throw new InvalidLetterboxdExportException("The file is not a valid ZIP archive.", exception);
        }
    }

    private static LetterboxdExport ReadArchive(ZipArchive archive)
    {
        // Only files in the root of the archive: deleted/diary.csv and orphaned/diary.csv are not part of the diary.
        if (archive.GetEntry("diary.csv") is null && archive.GetEntry("watchlist.csv") is null)
        {
            throw new InvalidLetterboxdExportException(
                "The ZIP archive has neither diary.csv nor watchlist.csv. " +
                "Please upload the archive as you get it from Letterboxd, without unpacking it.");
        }

        var warnings = new Warnings();
        var reviews = ReadReviews(archive);
        var likedFilms = ReadLikedFilms(archive);

        var diary = new List<LetterboxdDiaryEntry>();
        var rowNumber = 0;
        foreach (var record in ReadCsv(archive, "diary.csv"))
        {
            rowNumber++;
            var film = ParseFilm(record);
            if (film is null)
            {
                warnings.Add($"diary.csv, row {rowNumber}: skipped, the film has no name.");
                continue;
            }

            // "Date" is when the viewing was logged, "Watched Date" when it happened.
            var watchedOn = ParseDate(record["Watched Date"]) ?? ParseDate(record["Date"]);
            if (watchedOn is null)
            {
                warnings.Add($"diary.csv, row {rowNumber}: \"{film.Name}\" skipped, it has no valid date.");
                continue;
            }

            diary.Add(new LetterboxdDiaryEntry(
                film,
                watchedOn.Value,
                ParseRating(record["Rating"]),
                IsRewatch: record["Rewatch"].Trim().Equals("Yes", StringComparison.OrdinalIgnoreCase),
                reviews.GetValueOrDefault(record["Letterboxd URI"].Trim()),
                likedFilms.Contains(film)));
        }

        var watchlist = new List<LetterboxdWatchlistItem>();
        rowNumber = 0;
        foreach (var record in ReadCsv(archive, "watchlist.csv"))
        {
            rowNumber++;
            var film = ParseFilm(record);
            if (film is null)
            {
                warnings.Add($"watchlist.csv, row {rowNumber}: skipped, the film has no name.");
                continue;
            }

            var addedOn = ParseDate(record["Date"]);
            if (addedOn is null)
            {
                warnings.Add($"watchlist.csv, row {rowNumber}: \"{film.Name}\" skipped, it has no valid date.");
                continue;
            }

            watchlist.Add(new LetterboxdWatchlistItem(film, addedOn.Value));
        }

        return new LetterboxdExport(diary, watchlist, warnings.ToList());
    }

    /// <summary>Review texts by the Letterboxd URI of the viewing they belong to.</summary>
    private static Dictionary<string, string> ReadReviews(ZipArchive archive)
    {
        var reviews = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var record in ReadCsv(archive, "reviews.csv"))
        {
            var uri = record["Letterboxd URI"].Trim();
            var text = record["Review"].Trim();
            if (uri.Length > 0 && text.Length > 0)
            {
                reviews.TryAdd(uri, text);
            }
        }

        return reviews;
    }

    private static HashSet<LetterboxdFilm> ReadLikedFilms(ZipArchive archive) =>
        ReadCsv(archive, "likes/films.csv")
            .Select(ParseFilm)
            .OfType<LetterboxdFilm>()
            .ToHashSet();

    private static LetterboxdFilm? ParseFilm(CsvRecord record)
    {
        var name = record["Name"].Trim();
        if (name.Length == 0)
        {
            return null;
        }

        var year = int.TryParse(record["Year"].Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) && parsed is >= 1 and <= 9999
            ? parsed
            : (int?)null;
        return new LetterboxdFilm(name, year);
    }

    private static DateOnly? ParseDate(string value) =>
        DateOnly.TryParseExact(value.Trim(), DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : null;

    /// <summary>Letterboxd writes stars with up to one decimal ("3.5"); we store half stars.</summary>
    private static int? ParseRating(string value)
    {
        if (!decimal.TryParse(value.Trim(), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var stars))
        {
            return null;
        }

        var halfStars = stars * 2;
        return halfStars == decimal.Truncate(halfStars) && halfStars is >= 1 and <= 10 ? (int)halfStars : null;
    }

    /// <summary>The rows of a file in the archive, or nothing if the archive does not contain it.</summary>
    private static List<CsvRecord> ReadCsv(ZipArchive archive, string path)
    {
        var entry = archive.GetEntry(path);
        if (entry is null)
        {
            return [];
        }

        using var reader = new StreamReader(new MemoryStream(ReadBounded(entry)), Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var records = CsvReader.ReadRecords(reader).Take(MaxRows + 1).ToList();
        if (records.Count > MaxRows)
        {
            throw new InvalidLetterboxdExportException($"{path} in the ZIP archive has more than {MaxRows} rows.");
        }

        return records;
    }

    /// <summary>Guards against ZIP bombs: neither the announced nor the actual size may exceed the limit.</summary>
    private static byte[] ReadBounded(ZipArchiveEntry entry)
    {
        if (entry.Length > MaxFileBytes)
        {
            throw TooLarge(entry);
        }

        using var stream = entry.Open();
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = stream.Read(chunk)) > 0)
        {
            buffer.Write(chunk, 0, read);
            if (buffer.Length > MaxFileBytes)
            {
                throw TooLarge(entry);
            }
        }

        return buffer.ToArray();
    }

    private static InvalidLetterboxdExportException TooLarge(ZipArchiveEntry entry) =>
        new($"{entry.FullName} in the ZIP archive is larger than {MaxFileBytes / (1024 * 1024)} MB.");

    /// <summary>Collects the first few warnings and counts the rest, so a broken file cannot produce a huge response.</summary>
    private sealed class Warnings
    {
        private readonly List<string> _messages = [];
        private int _omitted;

        public void Add(string message)
        {
            if (_messages.Count < MaxWarnings)
            {
                _messages.Add(message);
            }
            else
            {
                _omitted++;
            }
        }

        public List<string> ToList() =>
            _omitted == 0 ? _messages : [.. _messages, $"... and {_omitted} more."];
    }
}
