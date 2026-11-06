using MovieTracker.Application.Users;
using MovieTracker.Domain.Users;

namespace MovieTracker.Application.Tests.Fakes;

public sealed class FakeUserRepository : IUserRepository
{
    public List<User> Users { get; } = [];

    public Task<User?> FindByGoogleSubjectAsync(string googleSubject, CancellationToken cancellationToken) =>
        Task.FromResult(Users.SingleOrDefault(user => user.GoogleSubject == googleSubject));

    public Task<User?> FindByIdAsync(Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult(Users.SingleOrDefault(user => user.Id == userId));

    public void Add(User user) => Users.Add(user);
}
