using System.ComponentModel.DataAnnotations;
using MovieTracker.Domain.Tags;

namespace MovieTracker.Api.Contracts;

/// <summary>
/// Validates a list of tags: at most <see cref="TagList.MaxTags"/> different ones, each at most
/// <see cref="TagList.MaxTagLength"/> characters long and without commas (the frontends separate tags with commas).
/// Blank tags are allowed; they are dropped when the tags are stored.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class ValidTagsAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is not IEnumerable<string?> tags)
        {
            return ValidationResult.Success;
        }

        var tagList = tags.ToList();
        if (tagList.Any(tag => tag is not null && tag.Contains(',')))
        {
            return Fail(validationContext, "A tag must not contain a comma.");
        }

        if (tagList.Any(tag => TagList.NormalizeTag(tag).Length > TagList.MaxTagLength))
        {
            return Fail(validationContext, $"A tag must not be longer than {TagList.MaxTagLength} characters.");
        }

        if (TagList.Normalize(tagList!).Length > TagList.MaxTags)
        {
            return Fail(validationContext, $"At most {TagList.MaxTags} different tags are allowed.");
        }

        return ValidationResult.Success;
    }

    private static ValidationResult Fail(ValidationContext context, string message) =>
        new(message, context.MemberName is { } member ? [member] : null);
}
