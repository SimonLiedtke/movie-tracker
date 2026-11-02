namespace MovieTracker.Api.Queries;

internal static class PagingExtensions
{
    public static IQueryable<T> Paginate<T>(this IQueryable<T> query, int page, int pageSize)
    {
        // long arithmetic: page * pageSize can overflow int for very large page numbers.
        var skip = (int)Math.Min((long)(page - 1) * pageSize, int.MaxValue);
        return query.Skip(skip).Take(pageSize);
    }
}
