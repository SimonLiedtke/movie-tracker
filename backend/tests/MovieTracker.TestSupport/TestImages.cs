namespace MovieTracker.TestSupport;

/// <summary>
/// The first bytes of real image files: enough for the format detection, not enough to show anything.
/// Each call returns a new array, so a test can change it.
/// </summary>
public static class TestImages
{
    /// <summary>JPEG start-of-image marker, then a JFIF header ("JFIF").</summary>
    public static byte[] Jpeg() => [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, .. "JFIF"u8];

    /// <summary>The eight bytes every PNG file starts with, then padding.</summary>
    public static byte[] Png() => [0x89, .. "PNG"u8, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00];

    /// <summary>A RIFF container of the type "WEBP" with a lossy ("VP8 ") chunk; the size field is not filled in correctly.</summary>
    public static byte[] WebP() => [.. "RIFF"u8, 0x1A, 0x00, 0x00, 0x00, .. "WEBP"u8, .. "VP8 "u8];

    /// <summary>A RIFF container that is not WebP: a WAV file's header.</summary>
    public static byte[] Wav() => [.. "RIFF"u8, 0x1A, 0x00, 0x00, 0x00, .. "WAVE"u8];

    /// <summary>The first four bytes of a PNG: a truncated signature.</summary>
    public static byte[] TruncatedPng() => Png()[..4];
}
