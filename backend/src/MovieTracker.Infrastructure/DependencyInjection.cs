using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MovieTracker.Application;
using MovieTracker.Application.Activity;
using MovieTracker.Application.Diary;
using MovieTracker.Application.Genres;
using MovieTracker.Application.Letterboxd;
using MovieTracker.Application.Movies;
using MovieTracker.Application.Tmdb;
using MovieTracker.Application.Users;
using MovieTracker.Application.Watchlist;
using MovieTracker.Infrastructure.Letterboxd;
using MovieTracker.Infrastructure.Persistence;
using MovieTracker.Infrastructure.Persistence.Repositories;
using MovieTracker.Infrastructure.Tmdb;

namespace MovieTracker.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, string connectionString, TmdbOptions tmdbOptions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentNullException.ThrowIfNull(tmdbOptions);

        services.AddDbContext<MovieTrackerDbContext>(options => options
            .UseNpgsql(PostgresConnectionString.Normalize(connectionString))
            .UseSnakeCaseNamingConvention());

        // The keys that encrypt the session cookie live in the database, so users stay signed in when the
        // app is redeployed or runs on a host without a persistent disk (e.g. Render).
        services.AddDataProtection().PersistKeysToDbContext<MovieTrackerDbContext>();

        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddScoped<IActivityLogRepository, ActivityLogRepository>();
        services.AddScoped<IDiaryRepository, DiaryRepository>();
        services.AddScoped<IGenreRepository, GenreRepository>();
        services.AddScoped<IMovieRepository, MovieRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IWatchlistRepository, WatchlistRepository>();

        services.AddSingleton(tmdbOptions);
        services.AddHttpClient<ITmdbClient, TmdbClient>();

        services.AddScoped<ILetterboxdImporter, LetterboxdImporter>();

        return services;
    }

    /// <summary>Brings the database up to date with the latest migration.</summary>
    public static async Task MigrateDatabaseAsync(this IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MovieTrackerDbContext>();
        await db.Database.MigrateAsync();
    }
}
