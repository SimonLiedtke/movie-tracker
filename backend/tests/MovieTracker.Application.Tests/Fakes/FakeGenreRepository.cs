using MovieTracker.Application.Genres;
using MovieTracker.Domain.Movies;

namespace MovieTracker.Application.Tests.Fakes;

public sealed class FakeGenreRepository : IGenreRepository
{
    public const int Action = 28;
    public const int Drama = 18;
    public const int Horror = 27;
    public const int ScienceFiction = 878;

    public List<Genre> Genres { get; } =
    [
        new(ScienceFiction, "Science Fiction"),
        new(Action, "Action"),
        new(Horror, "Horror"),
        new(Drama, "Drama"),
    ];

    public Task<IReadOnlyList<Genre>> ListAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Genre>>(Genres.OrderBy(genre => genre.Name).ToList());

    public Task<IReadOnlyList<Genre>> FindByIdsAsync(IReadOnlyCollection<int> genreIds, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Genre>>(Genres.Where(genre => genreIds.Contains(genre.Id)).ToList());
}
