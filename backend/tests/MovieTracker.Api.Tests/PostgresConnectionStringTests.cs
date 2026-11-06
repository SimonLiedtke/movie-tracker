using MovieTracker.Infrastructure.Persistence;
using Npgsql;

namespace MovieTracker.Api.Tests;

public sealed class PostgresConnectionStringTests
{
    [Fact]
    public void Converts_a_url_like_render_shows_it()
    {
        var result = new NpgsqlConnectionStringBuilder(PostgresConnectionString.Normalize(
            "postgresql://movie_tracker_user:p%40ss%3Aword@dpg-abc123-a/movie_tracker_xyz"));

        Assert.Equal("dpg-abc123-a", result.Host);
        Assert.Equal(5432, result.Port);
        Assert.Equal("movie_tracker_xyz", result.Database);
        Assert.Equal("movie_tracker_user", result.Username);
        Assert.Equal("p@ss:word", result.Password);
        Assert.Equal(GssEncryptionMode.Disable, result.GssEncryptionMode);
    }

    [Fact]
    public void Reads_the_port_and_ssl_mode_of_a_url()
    {
        var result = new NpgsqlConnectionStringBuilder(PostgresConnectionString.Normalize(
            "postgres://user:secret@db.example.com:6543/app?sslmode=require"));

        Assert.Equal((6543, SslMode.Require), (result.Port, result.SslMode));
    }

    [Fact]
    public void Keeps_a_connection_string_in_npgsql_format()
    {
        var result = new NpgsqlConnectionStringBuilder(PostgresConnectionString.Normalize(
            "Host=localhost;Port=5432;Database=movie_tracker;Username=movie_tracker;Password=movie_tracker"));

        Assert.Equal(("localhost", "movie_tracker", "movie_tracker"), (result.Host, result.Database, result.Password));
        Assert.Equal(GssEncryptionMode.Disable, result.GssEncryptionMode);
    }

    [Fact]
    public void Respects_an_explicit_gss_encryption_mode()
    {
        var result = new NpgsqlConnectionStringBuilder(PostgresConnectionString.Normalize(
            "Host=localhost;GSS Encryption Mode=Prefer"));

        Assert.Equal(GssEncryptionMode.Prefer, result.GssEncryptionMode);
    }
}
