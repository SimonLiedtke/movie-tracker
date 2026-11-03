using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MovieTracker.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(MovieTracker.Api.Tests.Infrastructure.ApiFactory))]

// All tests share one database, which ApiTestBase resets before each test.
[assembly: Xunit.v3.Parallelization(Mode = Xunit.Sdk.ParallelMode.None)]

namespace MovieTracker.Api.Tests.Infrastructure;

/// <summary>
/// Runs the API in memory against a throwaway PostgreSQL container (requires Docker).
/// One instance is shared by all tests; <see cref="ApiTestBase"/> resets the data before each test.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    /// <summary>"Now" for the API at the start of each test. Tests can move it with <see cref="TestClock.Advance"/>.</summary>
    public static readonly DateTimeOffset StartTime = new(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public TestClock Clock { get; } = new(StartTime);

    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync();

        // Starts the app, which applies the migrations (Development environment).
        _ = Server;

        // ResetAsync truncates tables: make sure that never happens to the local development database.
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MovieTrackerDbContext>();
        if (db.Database.GetConnectionString() != _postgres.GetConnectionString())
        {
            throw new InvalidOperationException("The API under test is not connected to the test container.");
        }
    }

    public async Task ResetAsync()
    {
        Clock.Set(StartTime);

        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MovieTrackerDbContext>();
        // Genres are seed data and stay.
        await db.Database.ExecuteSqlRawAsync(
            "TRUNCATE diary_entries, watchlist_items, movie_genres, movies RESTART IDENTITY CASCADE");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:MovieTracker", _postgres.GetConnectionString());
        builder.ConfigureTestServices(services => services.AddSingleton<TimeProvider>(Clock));
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}
