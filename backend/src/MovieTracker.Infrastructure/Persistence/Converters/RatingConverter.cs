using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using MovieTracker.Domain.Diary;

namespace MovieTracker.Infrastructure.Persistence.Converters;

/// <summary>
/// Stores a <see cref="Rating"/> as smallint (number of half stars).
/// EF Core handles null itself, so this converter also works for <c>Rating?</c>.
/// </summary>
internal sealed class RatingConverter() : ValueConverter<Rating, short>(
    rating => (short)rating.HalfStars,
    value => new Rating(value));
