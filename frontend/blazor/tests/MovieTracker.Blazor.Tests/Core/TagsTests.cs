namespace MovieTracker.Blazor.Tests.Core;

public class TagsTests
{
    [Fact]
    public void Tags_are_split_at_commas_and_trimmed()
    {
        Assert.Equal(["horror", "date night"], Tags.Parse(" horror ,date night,, ,"));
    }

    [Fact]
    public void No_text_is_no_tags()
    {
        Assert.Empty(Tags.Parse(""));
    }

    [Fact]
    public void Tags_are_formatted_as_the_text_of_a_field()
    {
        Assert.Equal("a, b", Tags.Format(["a", "b"]));
    }

    [Fact]
    public void Fine_tags_have_no_error()
    {
        Assert.Null(Tags.Error("cinema, date night"));
        Assert.Null(Tags.Error(""));
        Assert.Null(Tags.Error(new string('x', 50)));
    }

    [Fact]
    public void A_tag_that_is_too_long_is_refused()
    {
        Assert.Equal("A tag can have at most 50 characters.", Tags.Error(new string('x', 51)));
    }

    [Fact]
    public void Too_many_different_tags_are_refused()
    {
        var tags = string.Join(", ", Enumerable.Range(1, 21).Select(i => $"tag{i}"));

        Assert.Equal("At most 20 different tags are allowed.", Tags.Error(tags));
    }

    [Fact]
    public void Tags_that_the_server_stores_alike_count_once()
    {
        // 20 different ones, and duplicates by case and by spacing: still fine.
        var tags = string.Join(", ", Enumerable.Range(1, 20).Select(i => $"tag{i}")) + ", TAG1, tag  2";
        tags += ", date   night, Date Night";

        Assert.Equal("At most 20 different tags are allowed.", Tags.Error(tags));
        Assert.Null(Tags.Error(string.Join(", ", Enumerable.Range(1, 19).Select(i => $"tag{i}")) + ", Date Night, date  night, TAG1"));
    }
}
