using MovieTracker.Domain.Movies;
using MovieTracker.TestSupport;

namespace MovieTracker.Domain.Tests;

public sealed class MoviePosterTests
{
    private static readonly Guid MovieId = Guid.CreateVersion7();
    private static readonly DateTimeOffset Now = new(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Lists_the_accepted_content_types()
    {
        Assert.Equal(["image/jpeg", "image/png", "image/webp"], MoviePoster.ContentTypes.Order());
    }

    [Theory]
    [MemberData(nameof(AcceptedImages))]
    public void Accepts_jpeg_png_and_webp_and_knows_the_type_from_the_data(string expectedContentType, byte[] data)
    {
        var poster = MoviePoster.TryCreate(MovieId, data, Now);

        Assert.NotNull(poster);
        Assert.Equal(MovieId, poster.MovieId);
        Assert.Equal(expectedContentType, poster.ContentType);
        Assert.Equal(data, poster.Data);
        Assert.Equal(Now, poster.UpdatedAt);
        Assert.Contains(poster.ContentType, MoviePoster.ContentTypes);
    }

    public static TheoryData<string, byte[]> AcceptedImages => new()
    {
        { "image/jpeg", TestImages.Jpeg() },
        { "image/png", TestImages.Png() },
        { "image/webp", TestImages.WebP() },
    };

    [Theory]
    [MemberData(nameof(RejectedImages))]
    public void Rejects_data_that_is_not_a_supported_image(byte[] data)
    {
        Assert.Null(MoviePoster.TryCreate(MovieId, data, Now));
    }

    public static TheoryData<byte[]> RejectedImages => new()
    {
        Array.Empty<byte>(),
        "<svg xmlns=\"http://www.w3.org/2000/svg\"/>"u8.ToArray(),
        "GIF89a"u8.ToArray(),
        "just text"u8.ToArray(),
        // A RIFF container that is not WebP, e.g. a WAV file.
        TestImages.Wav(),
        TestImages.TruncatedPng(),
    };

    [Fact]
    public void Rejects_an_image_that_is_too_big()
    {
        var data = new byte[MoviePoster.MaxBytes + 1];
        TestImages.Jpeg().CopyTo(data, 0);

        Assert.Null(MoviePoster.TryCreate(MovieId, data, Now));
    }

    [Fact]
    public void Accepts_an_image_of_the_maximum_size()
    {
        var data = new byte[MoviePoster.MaxBytes];
        TestImages.Jpeg().CopyTo(data, 0);

        Assert.NotNull(MoviePoster.TryCreate(MovieId, data, Now));
    }

    [Fact]
    public void A_poster_belongs_to_a_movie()
    {
        Assert.Throws<ArgumentException>(() => MoviePoster.TryCreate(Guid.Empty, TestImages.Jpeg(), Now));
    }

    [Fact]
    public void Replaces_the_image_and_its_type()
    {
        var poster = MoviePoster.TryCreate(MovieId, TestImages.Jpeg(), Now)!;
        var later = Now.AddMinutes(5);

        var replaced = poster.TryReplace(TestImages.Png(), later);

        Assert.True(replaced);
        Assert.Equal("image/png", poster.ContentType);
        Assert.Equal(TestImages.Png(), poster.Data);
        Assert.Equal(later, poster.UpdatedAt);
    }

    [Fact]
    public void Keeps_the_image_if_the_new_one_is_not_acceptable()
    {
        var poster = MoviePoster.TryCreate(MovieId, TestImages.Jpeg(), Now)!;

        var replaced = poster.TryReplace("not an image"u8.ToArray(), Now.AddMinutes(5));

        Assert.False(replaced);
        Assert.Equal("image/jpeg", poster.ContentType);
        Assert.Equal(TestImages.Jpeg(), poster.Data);
        Assert.Equal(Now, poster.UpdatedAt);
    }
}
