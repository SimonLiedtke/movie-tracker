using System.ComponentModel.DataAnnotations;
using MovieTracker.Application.Movies;

namespace MovieTracker.Api.Contracts;

/// <summary>
/// A poster URL: an http(s) URL, or the path of an uploaded poster (<c>/api/movies/{id}/poster</c>), which is only
/// meaningful for the movie itself and is checked by the use case. Missing values are valid.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
internal sealed class PosterUrlAttribute() : ValidationAttribute("The {0} field is not a valid http(s) URL.")
{
    public override bool IsValid(object? value) =>
        value is not string text
        || MoviePosterUrl.IsExternal(text)
        || (text.StartsWith(MoviePosterUrl.PathPrefix, StringComparison.Ordinal) && text.AsSpan().IndexOfAny(" \t\r\n") < 0);
}
