using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using Microsoft.EntityFrameworkCore;
using MovieTracker.Infrastructure;
using MovieTracker.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("MovieTracker")
    ?? throw new InvalidOperationException("Connection string 'MovieTracker' is not configured.");

builder.Services.AddInfrastructure(connectionString);
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

if (app.Environment.IsDevelopment())
{
    // Generated from the code at /openapi/v1.json. Later compared against api/openapi.yaml in CI.
    app.MapOpenApi();

    // Convenient for local development only. Production deployments apply migrations separately.
    await using var scope = app.Services.CreateAsyncScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<MovieTrackerDbContext>();
    await dbContext.Database.MigrateAsync();
}

app.MapControllers();

app.Run();
