using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using Microsoft.EntityFrameworkCore;
using MovieTracker.Api.Auth;
using MovieTracker.Api.Imports;
using MovieTracker.Infrastructure;
using MovieTracker.Infrastructure.Persistence;
using MovieTracker.Infrastructure.Tmdb;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("MovieTracker")
    ?? throw new InvalidOperationException("Connection string 'MovieTracker' is not configured.");

var tmdbOptions = builder.Configuration.GetSection(TmdbOptions.SectionName).Get<TmdbOptions>() ?? new TmdbOptions();

builder.Services.AddInfrastructure(connectionString, tmdbOptions);
builder.Services.AddMovieTrackerAuthentication(builder.Configuration);
builder.Services.AddLetterboxdImport();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

builder.Services
    .AddControllers(options =>
        // Validation errors use the JSON property names ("genreIds") instead of the C# names ("GenreIds").
        options.ModelMetadataDetailsProviders.Add(new SystemTextJsonValidationMetadataProvider()))
    .AddJsonOptions(options =>
        // Unknown properties in request bodies result in 400 (additionalProperties: false in the spec).
        options.JsonSerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow);

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

// The built Angular app, if present in wwwroot (the Dockerfile in the repository root puts it there).
// In development, ng serve serves the frontend instead.
var hashedFileName = new Regex(@"-[A-Z0-9]{8}\.(js|css)$", RegexOptions.Compiled);
var frontendFiles = new StaticFileOptions
{
    OnPrepareResponse = context =>
    {
        // Angular puts a content hash into the names of its scripts and styles (main-ABCD1234.js), so those
        // never change under the same URL. Everything else (index.html, favicon.ico) is revalidated.
        context.Context.Response.Headers.CacheControl = hashedFileName.IsMatch(context.File.Name)
            ? "public, max-age=31536000, immutable"
            : "no-cache";
    },
};
app.UseStaticFiles(frontendFiles);

app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<CsrfHeaderMiddleware>();

if (app.Environment.IsDevelopment())
{
    // Generated from the code at /openapi/v1.json. Later compared against api/openapi.yaml in CI.
    app.MapOpenApi().AllowAnonymous();
}

// Always in development; elsewhere only if enabled (e.g. by docker-compose.yml), so real deployments
// can apply migrations separately.
if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    await using var scope = app.Services.CreateAsyncScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<MovieTrackerDbContext>();
    await dbContext.Database.MigrateAsync();
}

app.MapControllers();

// Angular routes like /diary/123 are not files: answer them with index.html, the router takes over.
app.MapFallbackToFile("index.html", frontendFiles).AllowAnonymous();
// More specific than the fallback above, so unknown API URLs get a 404 instead of the Angular app.
app.MapFallback("/api/{**path}", () => Results.NotFound());

app.Run();
