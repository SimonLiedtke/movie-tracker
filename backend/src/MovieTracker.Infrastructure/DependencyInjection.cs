using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MovieTracker.Application;
using MovieTracker.Application.Genres;
using MovieTracker.Application.Letterboxd;
using MovieTracker.Application.Movies;
using MovieTracker.Application.Tmdb;
using MovieTracker.Application.Users;
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

        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddScoped<IGenreRepository, GenreRepository>();
        services.AddScoped<IMovieRepository, MovieRepository>();
        services.AddScoped<IUserRepository, UserRepository>();

        services.AddSingleton(tmdbOptions);
        services.AddHttpClient<ITmdbClient, TmdbClient>();

        services.AddScoped<ILetterboxdImporter, LetterboxdImporter>();

        return services;
    }
}
