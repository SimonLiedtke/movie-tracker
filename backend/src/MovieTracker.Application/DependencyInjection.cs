using Microsoft.Extensions.DependencyInjection;
using MovieTracker.Application.Genres;

namespace MovieTracker.Application;

public static class DependencyInjection
{
    /// <summary>Registers the use cases. The ports they need are registered by the project that implements them.</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<GenreService>();
        return services;
    }
}
