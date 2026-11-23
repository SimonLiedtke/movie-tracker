using MovieTracker.Domain.Movies;

namespace MovieTracker.Domain.Tests;

public sealed class MoviePosterTests
{
    private static readonly Guid MovieId = Guid.CreateVersion7();
    private static readonly DateTimeOffset Now = new(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);

    private static byte[] Jpeg => [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46];

    private static byte[] Png => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00];

    private static byte[] WebP => [.. "RIFF"u8, 0x1A, 0x00, 0x00, 0x00, .. "WEBP"u8, .. "VP8 "u8];

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
        { "image/jpeg", Jpeg },
        { "image/png", Png },
        { "image/webp", WebP },
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
        Wav,
        // Shorter than the PNG signature.
        new byte[] { 0x89, 0x50, 0x4E, 0x47 },
    };

    private static byte[] Wav => [.. "RIFF"u8, 0x1A, 0x00, 0x00, 0x00, .. "WAVE"u8];

    [Fact]
    public void Rejects_an_image_that_is_too_big()
    {
        var data = new byte[MoviePoster.MaxBytes + 1];
        Jpeg.CopyTo(data, 0);

        Assert.Null(MoviePoster.TryCreate(MovieId, data, Now));
    }

    [Fact]
    public void Accepts_an_image_of_the_maximum_size()
    {
        var data = new byte[MoviePoster.MaxBytes];
        Jpeg.CopyTo(data, 0);

        Assert.NotNull(MoviePoster.TryCreate(MovieId, data, Now));
    }

    [Fact]
    public void A_poster_belongs_to_a_movie()
    {
        Assert.Throws<ArgumentException>(() => MoviePoster.TryCreate(Guid.Empty, Jpeg, Now));
    }

    [Fact]
    public void Replaces_the_image_and_its_type()
    {
        var poster = MoviePoster.TryCreate(MovieId, Jpeg, Now)!;
        var later = Now.AddMinutes(5);

        var replaced = poster.TryReplace(Png, later);

        Assert.True(replaced);
        Assert.Equal("image/png", poster.ContentType);
        Assert.Equal(Png, poster.Data);
        Assert.Equal(later, poster.UpdatedAt);
    }

    [Fact]
    public void Keeps_the_image_if_the_new_one_is_not_acceptable()
    {
        var poster = MoviePoster.TryCreate(MovieId, Jpeg, Now)!;

        var replaced = poster.TryReplace("not an image"u8.ToArray(), Now.AddMinutes(5));

        Assert.False(replaced);
        Assert.Equal("image/jpeg", poster.ContentType);
        Assert.Equal(Jpeg, poster.Data);
        Assert.Equal(Now, poster.UpdatedAt);
    }
}
