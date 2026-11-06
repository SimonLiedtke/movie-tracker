using Microsoft.Extensions.DependencyInjection;
using MovieTracker.Application.Diary;
using MovieTracker.Application.Genres;
using MovieTracker.Application.Movies;
using MovieTracker.Application.Users;

namespace MovieTracker.Application;

public static class DependencyInjection
{
    /// <summary>Registers the use cases. The ports they need are registered by the project that implements them.</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<DiaryService>();
        services.AddScoped<GenreService>();
        services.AddScoped<MovieService>();
        services.AddScoped<UserAccountService>();
        return services;
    }
}
