using Microsoft.AspNetCore.Components;
using MovieTracker.Blazor.Shared;
using MovieTracker.Blazor.Tests.Support;

namespace MovieTracker.Blazor.Tests.Features;

public class SharedComponentTests : BunitContext
{
    private IRenderedComponent<Pagination> RenderPagination(int page, int pageSize, int total, Action<int>? onPage = null) =>
        Render<Pagination>(parameters => parameters
            .Add(p => p.Page, page)
            .Add(p => p.PageSize, pageSize)
            .Add(p => p.TotalCount, total)
            .Add(p => p.OnPageChange, EventCallback.Factory.Create<int>(this, onPage ?? (_ => { }))));

    [Fact]
    public void One_page_has_no_pagination()
    {
        Assert.Empty(RenderPagination(1, 20, 20).FindAll("nav"));
        Assert.Empty(RenderPagination(1, 20, 0).FindAll("nav"));
    }

    [Theory]
    [InlineData(1, 3, "1 2 3")]
    [InlineData(1, 5, "1 2 … 5")]
    [InlineData(3, 5, "1 2 3 4 5")]
    [InlineData(1, 10, "1 2 … 10")]
    [InlineData(5, 10, "1 … 4 5 6 … 10")]
    [InlineData(10, 10, "1 … 9 10")]
    [InlineData(3, 10, "1 2 3 4 … 10")]
    public void The_first_the_last_and_the_pages_around_the_current_one_are_shown(int page, int pages, string expected)
    {
        var pagination = RenderPagination(page, 10, pages * 10);

        Assert.Equal(expected, string.Join(' ', pagination.FindAll(".pagination-list li").Select(li => li.Text())));
        Assert.Equal(page.ToString(), pagination.Find(".pagination-link.is-current").TextContent.Trim());
    }

    [Fact]
    public void Previous_and_next_are_disabled_at_the_ends()
    {
        var first = RenderPagination(1, 10, 30);
        Assert.True(first.Find(".pagination-previous").HasAttribute("disabled"));
        Assert.False(first.Find(".pagination-next").HasAttribute("disabled"));

        var last = RenderPagination(3, 10, 30);
        Assert.False(last.Find(".pagination-previous").HasAttribute("disabled"));
        Assert.True(last.Find(".pagination-next").HasAttribute("disabled"));
    }

    [Fact]
    public void The_buttons_ask_for_their_page()
    {
        var asked = new List<int>();
        var pagination = RenderPagination(2, 10, 50, asked.Add);

        pagination.Find(".pagination-previous").Click();
        pagination.Find(".pagination-next").Click();
        pagination.FindAll(".pagination-link").Last().Click();

        Assert.Equal([1, 3, 5], asked);
    }

    [Fact]
    public void A_poster_shows_the_image_with_the_title_as_alt_text()
    {
        var poster = Render<Poster>(p => p.Add(x => x.Url, "https://img.test/p.jpg").Add(x => x.Title, "Alien"));

        var image = poster.Find("img");
        Assert.Equal("https://img.test/p.jpg", image.GetAttribute("src"));
        Assert.Equal("Poster of Alien", image.GetAttribute("alt"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Without_a_url_a_poster_is_a_placeholder(string? url)
    {
        var poster = Render<Poster>(p => p.Add(x => x.Url, url));

        Assert.Empty(poster.FindAll("img"));
        Assert.Contains("🎬", poster.Find(".poster-placeholder").TextContent);
    }

    [Fact]
    public void An_image_that_fails_to_load_becomes_the_placeholder_until_the_url_changes()
    {
        var poster = Render<Poster>(p => p.Add(x => x.Url, "https://img.test/broken.jpg"));

        poster.Find("img").TriggerEvent("onerror", new Microsoft.AspNetCore.Components.Web.ErrorEventArgs());

        Assert.Empty(poster.FindAll("img"));
        Assert.NotEmpty(poster.FindAll(".poster-placeholder"));

        poster.Render(p => p.Add(x => x.Url, "https://img.test/other.jpg"));

        Assert.Equal("https://img.test/other.jpg", poster.Find("img").GetAttribute("src"));
    }

    [Theory]
    [InlineData(7, "★★★½", "3.5 of 5 stars")]
    [InlineData(1, "½", "0.5 of 5 stars")]
    [InlineData(10, "★★★★★", "5 of 5 stars")]
    public void A_rating_shows_stars_with_a_description(int halfStars, string stars, string description)
    {
        var rating = Render<Rating>(p => p.Add(x => x.HalfStars, halfStars));

        var span = rating.Find("span.rating-stars");
        Assert.Equal(stars, span.TextContent);
        Assert.Equal(description, span.GetAttribute("title"));
        Assert.Equal(description, span.GetAttribute("aria-label"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    public void Without_a_rating_nothing_is_shown(int? halfStars)
    {
        Assert.Empty(Render<Rating>(p => p.Add(x => x.HalfStars, halfStars)).Markup);
    }

    [Fact]
    public void An_error_shows_its_message_and_details_and_can_be_dismissed()
    {
        var dismissed = false;
        var error = Render<ErrorNotification>(p => p
            .Add(x => x.Error, new ApiError(400, "Validation failed", ["Too long.", "Too short."]))
            .Add(x => x.OnDismiss, EventCallback.Factory.Create(this, () => dismissed = true)));

        Assert.Equal("Validation failed", error.Find("p").TextContent);
        Assert.Equal(["• Too long.", "• Too short."], error.FindAll("li").Select(li => li.TextContent));

        error.Find("button.delete").Click();

        Assert.True(dismissed);
    }

    [Fact]
    public void Without_an_error_nothing_is_shown()
    {
        Assert.Empty(Render<ErrorNotification>().Markup);
    }
}
