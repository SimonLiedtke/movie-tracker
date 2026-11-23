namespace MovieTracker.Domain.Movies;

/// <summary>
/// An image that a user uploaded as the poster of their manually added movie. It is stored apart from the movie, so
/// that listing movies never loads image data. A movie has at most one.
/// </summary>
public sealed class MoviePoster
{
    /// <summary>A poster is shown at a few hundred pixels width, so this leaves room for even a large scan.</summary>
    public const int MaxBytes = 5 * 1024 * 1024;

    private MoviePoster()
    {
        // Required by EF Core.
    }

    private MoviePoster(Guid movieId, string contentType, byte[] data, DateTimeOffset updatedAt)
    {
        MovieId = movieId;
        ContentType = contentType;
        Data = data;
        UpdatedAt = updatedAt;
    }

    public Guid MovieId { get; private set; }

    /// <summary>One of <see cref="ContentTypes"/>, determined from the image itself, not taken from the upload.</summary>
    public string ContentType { get; private set; } = null!;

    public byte[] Data { get; private set; } = null!;

    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>The image formats that are accepted. No SVG: it can contain scripts.</summary>
    public static IReadOnlyList<string> ContentTypes { get; } = ["image/jpeg", "image/png", "image/webp"];

    /// <summary>Null if the data is empty, too big or not a JPEG, PNG or WebP image.</summary>
    public static MoviePoster? TryCreate(Guid movieId, byte[] data, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (movieId == Guid.Empty)
        {
            throw new ArgumentException("A poster belongs to a movie.", nameof(movieId));
        }

        return IsAcceptable(data, out var contentType) ? new MoviePoster(movieId, contentType, data, now) : null;
    }

    /// <summary>Replaces the image. Returns false (and changes nothing) if the data is not acceptable, see <see cref="TryCreate"/>.</summary>
    public bool TryReplace(byte[] data, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (!IsAcceptable(data, out var contentType))
        {
            return false;
        }

        ContentType = contentType;
        Data = data;
        UpdatedAt = now;
        return true;
    }

    private static bool IsAcceptable(byte[] data, out string contentType)
    {
        contentType = string.Empty;
        if (data.Length is 0 or > MaxBytes)
        {
            return false;
        }

        var detected = DetectContentType(data);
        if (detected is null)
        {
            return false;
        }

        contentType = detected;
        return true;
    }

    /// <summary>
    /// The format of the image according to its first bytes (a client's claim about the file type is not trusted),
    /// or null if it is none of <see cref="ContentTypes"/>.
    /// </summary>
    private static string? DetectContentType(ReadOnlySpan<byte> data)
    {
        ReadOnlySpan<byte> jpeg = [0xFF, 0xD8, 0xFF];
        ReadOnlySpan<byte> png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

        if (data.StartsWith(jpeg))
        {
            return "image/jpeg";
        }

        if (data.StartsWith(png))
        {
            return "image/png";
        }

        // WebP is a RIFF container: "RIFF", the size, then "WEBP".
        if (data.Length >= 12 && data[..4].SequenceEqual("RIFF"u8) && data[8..12].SequenceEqual("WEBP"u8))
        {
            return "image/webp";
        }

        return null;
    }
}
