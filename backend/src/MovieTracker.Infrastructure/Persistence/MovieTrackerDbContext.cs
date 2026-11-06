using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MovieTracker.Domain.Diary;
using MovieTracker.Domain.Movies;
using MovieTracker.Domain.Users;
using MovieTracker.Domain.Watchlist;

namespace MovieTracker.Infrastructure.Persistence;

public sealed class MovieTrackerDbContext(DbContextOptions<MovieTrackerDbContext> options) : DbContext(options), IDataProtectionKeyContext
{
    public DbSet<Movie> Movies => Set<Movie>();

    public DbSet<Genre> Genres => Set<Genre>();

    public DbSet<DiaryEntry> DiaryEntries => Set<DiaryEntry>();

    public DbSet<WatchlistItem> WatchlistItems => Set<WatchlistItem>();

    public DbSet<User> Users => Set<User>();

    /// <summary>ASP.NET Core data protection keys, which encrypt the session cookies.</summary>
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.ApplyConfigurationsFromAssembly(typeof(MovieTrackerDbContext).Assembly);
}
