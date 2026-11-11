using MovieTracker.Domain.Tags;

namespace MovieTracker.Domain.Tests;

public sealed class TagListTests
{
    [Fact]
    public void Normalize_trims_lowercases_and_sorts()
    {
        Assert.Equal(["comedy", "sci-fi"], TagList.Normalize(["  Sci-Fi ", "COMEDY"]));
    }

    [Fact]
    public void Normalize_drops_blanks_and_duplicates_whatever_their_case()
    {
        Assert.Equal(["horror"], TagList.Normalize(["Horror", "horror", " HORROR ", "", "   ", null!]));
    }

    [Fact]
    public void Normalize_shortens_runs_of_spaces_inside_a_tag()
    {
        Assert.Equal(["date night"], TagList.Normalize(["Date    Night"]));
    }

    [Fact]
    public void Normalize_of_nothing_is_empty()
    {
        Assert.Empty(TagList.Normalize(null));
        Assert.Empty(TagList.Normalize([]));
    }

    [Fact]
    public void Merge_keeps_the_tags_of_both_lists_once()
    {
        Assert.Equal(["a", "b", "c"], TagList.Merge(["b", "A"], ["c", "B"]));
    }

    [Fact]
    public void Merge_does_not_drop_tags_beyond_the_maximum()
    {
        var first = Enumerable.Range(0, TagList.MaxTags).Select(i => $"first {i}");
        var second = Enumerable.Range(0, TagList.MaxTags).Select(i => $"second {i}");

        Assert.Equal(2 * TagList.MaxTags, TagList.Merge(first, second).Length);
    }
}
