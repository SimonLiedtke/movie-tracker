using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using Microsoft.EntityFrameworkCore;
using MovieTracker.Api.Auth;
using MovieTracker.Infrastructure;
using MovieTracker.Infrastructure.Persistence;
using MovieTracker.Infrastructure.Tmdb;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("MovieTracker")
    ?? throw new InvalidOperationException("Connection string 'MovieTracker' is not configured.");

var tmdbOptions = builder.Configuration.GetSection(TmdbOptions.SectionName).Get<TmdbOptions>() ?? new TmdbOptions();

builder.Services.AddInfrastructure(connectionString, tmdbOptions);
builder.Services.AddMovieTrackerAuthentication(builder.Configuration);
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

app.Run();
