namespace MovieTracker.Application.Movies;

/// <summary>
/// The poster URL of a movie is either an external one (http or https), typed in by the user or from TMDB, or the
/// path of the movie's uploaded poster image on this server.
/// </summary>
public static class MoviePosterUrl
{
    /// <summary>What every uploaded poster's URL starts with: the path of the movies API.</summary>
    public const string PathPrefix = "/api/movies/";

    /// <summary>Where the API serves the uploaded poster of the movie. Relative, so it works on any host.</summary>
    public static string For(Guid movieId) => $"{PathPrefix}{movieId}/poster";

    public static bool IsExternal(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https";
}
