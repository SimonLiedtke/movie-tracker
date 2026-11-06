using System.IO.Compression;
using System.Text;

namespace MovieTracker.Api.Tests.Infrastructure;

/// <summary>Builds Letterboxd export archives for tests, in the format of the real export (CRLF line breaks, header rows).</summary>
public static class LetterboxdZip
{
    private const string DiaryHeader = "Date,Name,Year,Letterboxd URI,Rating,Rewatch,Tags,Watched Date";
    private const string WatchlistHeader = "Date,Name,Year,Letterboxd URI";
    private const string ReviewsHeader = "Date,Name,Year,Letterboxd URI,Rating,Rewatch,Review,Tags,Watched Date";
    private const string LikedFilmsHeader = "Date,Name,Year,Letterboxd URI";

    /// <summary>The rows without the header row. Files without rows are left out of the archive.</summary>
    public static MemoryStream Create(
        IEnumerable<string>? diary = null,
        IEnumerable<string>? watchlist = null,
        IEnumerable<string>? reviews = null,
        IEnumerable<string>? likedFilms = null)
    {
        var files = new List<(string Path, string Content)>();
        AddCsv(files, "diary.csv", DiaryHeader, diary);
        AddCsv(files, "watchlist.csv", WatchlistHeader, watchlist);
        AddCsv(files, "reviews.csv", ReviewsHeader, reviews);
        AddCsv(files, "likes/films.csv", LikedFilmsHeader, likedFilms);
        return CreateFromFiles(files.ToArray());
    }

    public static MemoryStream CreateFromFiles(params (string Path, string Content)[] files)
    {
        var buffer = new MemoryStream();
        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (path, content) in files)
            {
                using var writer = new StreamWriter(archive.CreateEntry(path).Open(), new UTF8Encoding(false));
                writer.Write(content);
            }
        }

        buffer.Position = 0;
        return buffer;
    }

    /// <param name="watchedOn">Null: the viewing has no "Watched Date", only the date it was logged.</param>
    public static string DiaryRow(
        string name,
        int? year,
        string? watchedOn = "2026-10-01",
        string rating = "",
        bool rewatch = false,
        string uri = "https://boxd.it/entry",
        string loggedOn = "2026-10-02") =>
        Row(loggedOn, name, year?.ToString() ?? "", uri, rating, rewatch ? "Yes" : "", "", watchedOn ?? "");

    public static string WatchlistRow(string name, int? year, string addedOn = "2026-09-01") =>
        Row(addedOn, name, year?.ToString() ?? "", "https://boxd.it/film");

    public static string ReviewRow(string name, int? year, string uri, string review, string watchedOn = "2026-10-01") =>
        Row("2026-10-02", name, year?.ToString() ?? "", uri, "", "", review, "", watchedOn);

    public static string LikedFilmRow(string name, int? year) =>
        Row("2026-10-02", name, year?.ToString() ?? "", "https://boxd.it/film");

    /// <summary>Joins the fields to a CSV row, in quotes where the format requires it.</summary>
    public static string Row(params string[] fields) => string.Join(',', fields.Select(Quote));

    private static string Quote(string field) =>
        field.AsSpan().IndexOfAny(",\"\r\n") >= 0 ? $"\"{field.Replace("\"", "\"\"")}\"" : field;

    private static void AddCsv(List<(string Path, string Content)> files, string path, string header, IEnumerable<string>? rows)
    {
        if (rows is not null)
        {
            files.Add((path, string.Join("\r\n", rows.Prepend(header)) + "\r\n"));
        }
    }
}
