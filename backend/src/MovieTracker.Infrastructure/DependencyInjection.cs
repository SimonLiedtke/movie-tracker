using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MovieTracker.Application.Letterboxd;
using MovieTracker.Application.Tmdb;
using MovieTracker.Infrastructure.Letterboxd;
using MovieTracker.Infrastructure.Persistence;
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

        services.AddSingleton(tmdbOptions);
        services.AddHttpClient<ITmdbClient, TmdbClient>();

        services.AddScoped<ILetterboxdImporter, LetterboxdImporter>();

        return services;
    }
}
