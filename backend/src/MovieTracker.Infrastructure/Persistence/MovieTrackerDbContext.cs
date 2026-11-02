using Microsoft.EntityFrameworkCore;
using MovieTracker.Domain.Diary;
using MovieTracker.Domain.Movies;
using MovieTracker.Domain.Watchlist;

namespace MovieTracker.Infrastructure.Persistence;

public sealed class MovieTrackerDbContext(DbContextOptions<MovieTrackerDbContext> options) : DbContext(options)
{
    public DbSet<Movie> Movies => Set<Movie>();

    public DbSet<Genre> Genres => Set<Genre>();

    public DbSet<DiaryEntry> DiaryEntries => Set<DiaryEntry>();

    public DbSet<WatchlistItem> WatchlistItems => Set<WatchlistItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.ApplyConfigurationsFromAssembly(typeof(MovieTrackerDbContext).Assembly);
}
