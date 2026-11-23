using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace MovieTracker.Blazor;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the API clients and the auth service. Relative URLs (/api/...) are resolved against
    /// <paramref name="baseAddress"/>, which serves the API as well: the session cookie only works on the same origin.
    /// </summary>
    /// <param name="innerHandler">What actually sends the requests; the browser's fetch by default. Tests pass a fake backend.</param>
    public static IServiceCollection AddMovieTracker(
        this IServiceCollection services, Uri baseAddress, Func<HttpMessageHandler>? innerHandler = null)
    {
        services.AddSingleton<AuthService>();
        services.AddSingleton(provider =>
        {
            var handler = new ApiHandler(provider.GetRequiredService<AuthService>)
            {
                InnerHandler = innerHandler?.Invoke() ?? new HttpClientHandler(),
            };
            return new ApiClient(new HttpClient(handler) { BaseAddress = baseAddress });
        });
        services.AddSingleton<Api>();
        return services;
    }
}
