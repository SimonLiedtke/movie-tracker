namespace MovieTracker.Infrastructure.Tmdb;

/// <summary>Settings from the "Tmdb" configuration section.</summary>
public sealed class TmdbOptions
{
    public const string SectionName = "Tmdb";

    /// <summary>The "API Read Access Token" from https://www.themoviedb.org/settings/api. Without it, TMDB search is off.</summary>
    public string? AccessToken { get; init; }

    /// <summary>Language of titles and overviews, e.g. "en-US" or "de-DE".</summary>
    public string Language { get; init; } = "en-US";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(AccessToken);
}
