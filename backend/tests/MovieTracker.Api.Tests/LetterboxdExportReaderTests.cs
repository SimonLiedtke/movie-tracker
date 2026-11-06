using System.IO.Compression;
using System.Text;
using MovieTracker.Application.Letterboxd;
using static MovieTracker.TestSupport.LetterboxdZip;

namespace MovieTracker.Api.Tests;

public sealed class LetterboxdExportReaderTests
{
    private static readonly LetterboxdFilm Alien = new("Alien", 1979);

    [Fact]
    public void Reads_the_diary()
    {
        using var zip = Create(diary:
        [
            DiaryRow("Alien", 1979, watchedOn: "2026-09-30", rating: "4.5", rewatch: true),
            DiaryRow("Aliens", 1986, watchedOn: "2026-10-05"),
        ]);

        var export = LetterboxdExportReader.Read(zip);

        Assert.Equal(
            [
                new LetterboxdDiaryEntry(Alien, new DateOnly(2026, 9, 30), 9, IsRewatch: true, Review: null, Liked: false),
                new LetterboxdDiaryEntry(new LetterboxdFilm("Aliens", 1986), new DateOnly(2026, 10, 5), null, false, null, false),
            ],
            export.Diary);
        Assert.Empty(export.Watchlist);
        Assert.Empty(export.Warnings);
    }

    [Theory]
    [InlineData("0.5", 1)]
    [InlineData("3", 6)]
    [InlineData("3.5", 7)]
    [InlineData("5", 10)]
    [InlineData("", null)]
    [InlineData("0", null)]
    [InlineData("5.5", null)]
    [InlineData("3.25", null)]
    [InlineData("many", null)]
    public void Converts_stars_to_half_stars(string rating, int? expected)
    {
        using var zip = Create(diary: [DiaryRow("Alien", 1979, rating: rating)]);

        var export = LetterboxdExportReader.Read(zip);

        Assert.Equal(expected, Assert.Single(export.Diary).Rating);
    }

    [Fact]
    public void Reads_names_with_commas_and_quotes()
    {
        using var zip = Create(
            diary:
            [
                DiaryRow("Me, Myself & Irene", 2000),
                DiaryRow("\"Crocodile\" Dundee", 1986),
            ]);

        var export = LetterboxdExportReader.Read(zip);

        Assert.Equal(["Me, Myself & Irene", "\"Crocodile\" Dundee"], export.Diary.Select(entry => entry.Film.Name));
    }

    [Fact]
    public void Uses_the_logged_date_if_the_watched_date_is_missing()
    {
        using var zip = Create(diary: [DiaryRow("Alien", 1979, watchedOn: null, loggedOn: "2026-10-02")]);

        var export = LetterboxdExportReader.Read(zip);

        Assert.Equal(new DateOnly(2026, 10, 2), Assert.Single(export.Diary).WatchedOn);
    }

    [Fact]
    public void Takes_reviews_from_reviews_csv_by_the_uri_of_the_viewing()
    {
        // Reviews can span lines, and the same film can be watched twice: only the URI tells the viewings apart.
        using var zip = Create(
            diary:
            [
                DiaryRow("Alien", 1979, watchedOn: "2026-09-01", uri: "https://boxd.it/first"),
                DiaryRow("Alien", 1979, watchedOn: "2026-10-01", uri: "https://boxd.it/second"),
            ],
            reviews: [ReviewRow("Alien", 1979, "https://boxd.it/second", "Scary.\r\nStill, \"great\".", watchedOn: "2026-10-01")]);

        var export = LetterboxdExportReader.Read(zip);

        Assert.Equal([null, "Scary.\r\nStill, \"great\"."], export.Diary.Select(entry => entry.Review));
    }

    [Fact]
    public void Marks_all_viewings_of_a_liked_film_as_liked()
    {
        using var zip = Create(
            diary:
            [
                DiaryRow("Alien", 1979, watchedOn: "2026-09-01"),
                DiaryRow("Alien", 1979, watchedOn: "2026-10-01"),
                DiaryRow("Aliens", 1986),
            ],
            likedFilms: [LikedFilmRow("Alien", 1979)]);

        var export = LetterboxdExportReader.Read(zip);

        Assert.Equal([true, true, false], export.Diary.Select(entry => entry.Liked));
    }

    [Fact]
    public void Reads_the_watchlist()
    {
        using var zip = Create(watchlist: [WatchlistRow("Alien", 1979, addedOn: "2026-08-15"), WatchlistRow("Unknown year", null)]);

        var export = LetterboxdExportReader.Read(zip);

        Assert.Empty(export.Diary);
        Assert.Equal(
            [
                new LetterboxdWatchlistItem(Alien, new DateOnly(2026, 8, 15)),
                new LetterboxdWatchlistItem(new LetterboxdFilm("Unknown year", null), new DateOnly(2026, 9, 1)),
            ],
            export.Watchlist);
    }

    [Fact]
    public void Ignores_the_files_in_subfolders()
    {
        // Deleted and orphaned viewings have a diary.csv of their own.
        using var zip = CreateFromFiles(
            ("diary.csv", "Date,Name,Year,Letterboxd URI,Rating,Rewatch,Tags,Watched Date\r\n"
                + DiaryRow("Alien", 1979) + "\r\n"),
            ("deleted/diary.csv", "Date,Name,Year,Letterboxd URI,Rating,Rewatch,Tags,Watched Date\r\n"
                + DiaryRow("Deleted", 2000) + "\r\n"),
            ("profile.csv", "Date Joined,Username,Email Address\r\n2020-01-01,someone,someone@example.com\r\n"));

        var export = LetterboxdExportReader.Read(zip);

        Assert.Equal(["Alien"], export.Diary.Select(entry => entry.Film.Name));
    }

    [Fact]
    public void Skips_rows_that_cannot_be_used_and_says_why()
    {
        using var zip = Create(
            diary:
            [
                DiaryRow("Alien", 1979),
                DiaryRow("Aliens", 1986, watchedOn: "yesterday", loggedOn: "also not a date"),
                DiaryRow("", 2000),
            ],
            watchlist: [WatchlistRow("Heat", 1995, addedOn: "")]);

        var export = LetterboxdExportReader.Read(zip);

        Assert.Equal(["Alien"], export.Diary.Select(entry => entry.Film.Name));
        Assert.Empty(export.Watchlist);
        Assert.Equal(
            [
                "diary.csv, row 2: \"Aliens\" skipped, it has no valid date.",
                "diary.csv, row 3: skipped, the film has no name.",
                "watchlist.csv, row 1: \"Heat\" skipped, it has no valid date.",
            ],
            export.Warnings);
    }

    [Fact]
    public void Shortens_a_long_list_of_warnings()
    {
        using var zip = Create(diary: Enumerable.Range(0, 30).Select(_ => DiaryRow("", 2000)));

        var export = LetterboxdExportReader.Read(zip);

        Assert.Equal(21, export.Warnings.Count);
        Assert.Equal("... and 10 more.", export.Warnings[^1]);
    }

    [Fact]
    public void Accepts_an_export_with_only_a_watchlist()
    {
        using var zip = Create(watchlist: [WatchlistRow("Alien", 1979)]);

        var export = LetterboxdExportReader.Read(zip);

        Assert.Empty(export.Diary);
        Assert.Single(export.Watchlist);
    }

    [Fact]
    public void Understands_a_byte_order_mark_and_unix_line_breaks()
    {
        var csv = "﻿Date,Name,Year,Letterboxd URI\n2026-09-01,Alien,1979,https://boxd.it/film\n";
        using var zip = CreateFromFiles(("watchlist.csv", csv));

        var export = LetterboxdExportReader.Read(zip);

        Assert.Equal(Alien, Assert.Single(export.Watchlist).Film);
    }

    [Fact]
    public void Treats_a_year_that_is_not_a_year_as_unknown()
    {
        using var zip = Create(diary: [DiaryRow("Alien", null), Row("2026-10-02", "Aliens", "99999", "x", "", "", "", "2026-10-01")]);

        var export = LetterboxdExportReader.Read(zip);

        Assert.Equal([null, null], export.Diary.Select(entry => entry.Film.Year));
    }

    [Theory]
    [InlineData("")]
    [InlineData("Date,Name,Year\n")]
    [InlineData("Date,Name,Year,Letterboxd URI\n2026-09-01,Alien,1979,https://boxd.it/film\n")]
    public void Rejects_a_file_that_is_not_a_zip_archive(string content)
    {
        using var notAZip = new MemoryStream(Encoding.UTF8.GetBytes(content));

        var exception = Assert.Throws<InvalidLetterboxdExportException>(() => LetterboxdExportReader.Read(notAZip));

        Assert.Contains("ZIP", exception.Message);
    }

    [Fact]
    public void Rejects_a_truncated_archive()
    {
        using var zip = Create(diary: [DiaryRow("Alien", 1979)]);
        using var truncated = new MemoryStream(zip.ToArray()[..(int)(zip.Length / 2)]);

        Assert.Throws<InvalidLetterboxdExportException>(() => LetterboxdExportReader.Read(truncated));
    }

    [Fact]
    public void Rejects_an_archive_without_diary_and_watchlist()
    {
        using var zip = CreateFromFiles(("profile.csv", "Username\r\nsomeone\r\n"), ("lists/horror.csv", "x\r\n"));

        var exception = Assert.Throws<InvalidLetterboxdExportException>(() => LetterboxdExportReader.Read(zip));

        Assert.Contains("diary.csv", exception.Message);
    }

    [Fact]
    public void Rejects_a_file_that_is_far_too_large_when_unpacked()
    {
        // A ZIP bomb: tiny packed, huge unpacked.
        using var zip = CreateFromFiles(("diary.csv", new string('x', 21 * 1024 * 1024)));

        var exception = Assert.Throws<InvalidLetterboxdExportException>(() => LetterboxdExportReader.Read(zip));

        Assert.Contains("larger than", exception.Message);
    }

    [Fact]
    public void Rejects_a_file_with_far_too_many_rows()
    {
        using var zip = Create(watchlist: Enumerable.Range(0, 20_001).Select(i => WatchlistRow($"Film {i}", 2000)));

        var exception = Assert.Throws<InvalidLetterboxdExportException>(() => LetterboxdExportReader.Read(zip));

        Assert.Contains("watchlist.csv", exception.Message);
        Assert.Contains("more than 20000 rows", exception.Message);
    }

    [Fact]
    public void Accepts_a_file_with_the_largest_number_of_rows()
    {
        using var zip = Create(watchlist: Enumerable.Range(0, 20_000).Select(i => WatchlistRow($"Film {i}", 2000)));

        Assert.Equal(20_000, LetterboxdExportReader.Read(zip).Watchlist.Count);
    }

    [Fact]
    public void Leaves_the_stream_open()
    {
        using var zip = Create(diary: [DiaryRow("Alien", 1979)]);

        LetterboxdExportReader.Read(zip);

        Assert.True(zip.CanRead);
    }

    [Fact]
    public void Reads_the_archive_independent_of_the_compression()
    {
        using var buffer = new MemoryStream();
        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            using var writer = new StreamWriter(archive.CreateEntry("watchlist.csv", CompressionLevel.NoCompression).Open());
            writer.Write("Date,Name,Year,Letterboxd URI\r\n2026-09-01,Alien,1979,x\r\n");
        }

        buffer.Position = 0;
        var export = LetterboxdExportReader.Read(buffer);

        Assert.Single(export.Watchlist);
    }
}
