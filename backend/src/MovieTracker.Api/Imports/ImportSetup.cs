namespace MovieTracker.Api.Imports;

internal static class ImportSetup
{
    public static IServiceCollection AddLetterboxdImport(this IServiceCollection services)
    {
        services.AddSingleton<LetterboxdImportQueue>();
        services.AddScoped<LetterboxdImporter>();
        services.AddHostedService<LetterboxdImportWorker>();
        return services;
    }
}
