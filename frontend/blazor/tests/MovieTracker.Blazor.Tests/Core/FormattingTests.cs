namespace MovieTracker.Blazor.Tests.Core;

public class FormattingTests
{
    [Theory]
    [InlineData(null, "")]
    [InlineData(0, "")]
    [InlineData(1, "½")]
    [InlineData(2, "★")]
    [InlineData(7, "★★★½")]
    [InlineData(10, "★★★★★")]
    public void Ratings_are_shown_as_stars(int? halfStars, string expected)
    {
        Assert.Equal(expected, RatingFormat.Format(halfStars));
    }

    [Fact]
    public void The_rating_options_go_from_half_a_star_to_five_stars()
    {
        Assert.Equal(10, RatingFormat.Options.Count);
        Assert.Equal(new RatingOption(1, "½ (0.5)"), RatingFormat.Options[0]);
        Assert.Equal(new RatingOption(3, "★½ (1.5)"), RatingFormat.Options[2]);
        Assert.Equal(new RatingOption(10, "★★★★★ (5)"), RatingFormat.Options[9]);
    }

    [Theory]
    [InlineData(7, "3.5 of 5 stars")]
    [InlineData(10, "5 of 5 stars")]
    [InlineData(1, "0.5 of 5 stars")]
    public void A_rating_is_described_in_stars(int halfStars, string expected)
    {
        Assert.Equal(expected, RatingFormat.Describe(halfStars));
    }

    [Fact]
    public void Dates_are_english()
    {
        var date = new DateOnly(2026, 11, 7);

        Assert.Equal("2026-11-07", Dates.Iso(date));
        Assert.Equal("2026-11", Dates.MonthOf(date));
        Assert.Equal("November 2026", Dates.FormatMonth(date));
        Assert.Equal("Sat 7", Dates.FormatDay(date));
        Assert.Equal("November 7, 2026", Dates.FormatDate(date));
        Assert.Equal("1999", Dates.YearOf(new DateOnly(1999, 1, 1)));
        Assert.Equal("", Dates.YearOf(null));
    }

    [Fact]
    public void A_date_text_is_formatted_or_left_alone()
    {
        Assert.Equal("May 25, 1979", Dates.FormatDate("1979-05-25"));
        Assert.Equal("not a date", Dates.FormatDate("not a date"));
    }

    [Fact]
    public void A_point_in_time_is_shown_in_the_local_time_zone()
    {
        var timestamp = new DateTimeOffset(2026, 11, 7, 21, 30, 0, TimeSpan.Zero);

        var local = timestamp.ToLocalTime();
        Assert.Equal(
            $"Nov {local.Day}, 2026, {(local.Hour % 12 == 0 ? 12 : local.Hour % 12)}:{local.Minute:00} {(local.Hour < 12 ? "AM" : "PM")}",
            Dates.FormatDateTime(timestamp));
    }

    [Theory]
    [InlineData("12", 12)]
    [InlineData("-3", -3)]
    [InlineData("0", null)]
    [InlineData("abc", null)]
    [InlineData("1.5", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void Numbers_that_are_none_are_left_out(string? text, int? expected)
    {
        Assert.Equal(expected, Parse.Number(text));
    }

    [Theory]
    [InlineData("90", 90)]
    [InlineData("-5", null)]
    [InlineData("x", null)]
    public void Minutes_are_positive_whole_numbers(string text, int? expected)
    {
        Assert.Equal(expected, Parse.Minutes(text));
    }

    [Theory]
    [InlineData("3", 3)]
    [InlineData("x", 1)]
    [InlineData(null, 1)]
    public void The_page_defaults_to_one(string? text, int expected)
    {
        Assert.Equal(expected, Parse.Page(text));
    }
}
