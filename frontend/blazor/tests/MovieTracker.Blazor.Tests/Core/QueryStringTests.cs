namespace MovieTracker.Blazor.Tests.Core;

public class QueryStringTests
{
    [Fact]
    public void Build_leaves_out_empty_values()
    {
        var query = QueryString.Build(("a", "1"), ("b", null), ("c", ""), ("d", 0), ("e", new int[0]));

        Assert.Equal("?a=1&d=0", query);
    }

    [Fact]
    public void Build_without_values_is_empty()
    {
        Assert.Equal("", QueryString.Build(("a", null)));
    }

    [Fact]
    public void Build_repeats_the_parameter_for_a_collection()
    {
        Assert.Equal("?genreId=1&genreId=2", QueryString.Build(("genreId", new[] { 1, 2 })));
    }

    [Fact]
    public void Build_escapes_keys_and_values()
    {
        Assert.Equal("?q=a%20%26%20b%3Dc", QueryString.Build(("q", "a & b=c")));
    }

    [Fact]
    public void Merge_sets_replaces_and_removes_parameters()
    {
        var merged = QueryString.Merge("?a=1&b=2&page=3", [("b", "x"), ("c", 5), ("page", null)]);

        Assert.Equal("?a=1&b=x&c=5", merged);
    }

    [Fact]
    public void Merge_replaces_all_values_of_a_repeated_parameter()
    {
        var merged = QueryString.Merge("?genreId=1&genreId=2&a=1", [("genreId", new[] { 3 })]);

        Assert.Equal("?a=1&genreId=3", merged);
    }

    [Fact]
    public void Merge_removes_a_parameter_set_to_an_empty_collection()
    {
        Assert.Equal("?a=1", QueryString.Merge("?a=1&genreId=1&genreId=2", [("genreId", new List<int>())]));
    }

    [Fact]
    public void Merge_reads_encoded_parameters()
    {
        Assert.Equal("?search=a%20b&page=2", QueryString.Merge("?search=a%20b", [("page", 2)]));
        Assert.Equal("?search=a%20b", QueryString.Merge("?search=a+b", []));
    }

    [Fact]
    public void Parse_reads_repeated_parameters_in_order()
    {
        var pairs = QueryString.Parse("?a=1&b&a=2");

        Assert.Equal(
            [new("a", "1"), new("b", ""), new("a", "2")],
            pairs);
    }
}
