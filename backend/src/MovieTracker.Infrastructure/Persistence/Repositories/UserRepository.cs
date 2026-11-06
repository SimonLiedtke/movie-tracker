using Microsoft.EntityFrameworkCore;
using MovieTracker.Application.Users;
using MovieTracker.Domain.Users;

namespace MovieTracker.Infrastructure.Persistence.Repositories;

internal sealed class UserRepository(MovieTrackerDbContext db) : IUserRepository
{
    public async Task<User?> FindByGoogleSubjectAsync(string googleSubject, CancellationToken cancellationToken) =>
        await db.Users.SingleOrDefaultAsync(user => user.GoogleSubject == googleSubject, cancellationToken);

    public async Task<User?> FindByIdAsync(Guid userId, CancellationToken cancellationToken) =>
        await db.Users.AsNoTracking().SingleOrDefaultAsync(user => user.Id == userId, cancellationToken);

    public void Add(User user) => db.Users.Add(user);
}
