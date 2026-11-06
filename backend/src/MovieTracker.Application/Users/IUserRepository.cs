using MovieTracker.Domain.Users;

namespace MovieTracker.Application.Users;

public interface IUserRepository
{
    /// <summary>For changing the user: changes are saved with <see cref="IUnitOfWork"/>.</summary>
    Task<User?> FindByGoogleSubjectAsync(string googleSubject, CancellationToken cancellationToken);

    /// <summary>For reading only.</summary>
    Task<User?> FindByIdAsync(Guid userId, CancellationToken cancellationToken);

    void Add(User user);
}
