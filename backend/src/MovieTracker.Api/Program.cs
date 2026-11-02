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

app.Run();
