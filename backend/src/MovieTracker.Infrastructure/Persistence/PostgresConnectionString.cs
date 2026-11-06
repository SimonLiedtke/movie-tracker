using Npgsql;

namespace MovieTracker.Infrastructure.Persistence;

public static class PostgresConnectionString
{
    /// <summary>
    /// Accepts a connection string in Npgsql's format (<c>Host=…;Username=…</c>) or as URL
    /// (<c>postgresql://user:password@host:5432/database</c>, as shown by hosting providers like Render).
    /// </summary>
    public static string Normalize(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        var builder = IsUrl(connectionString)
            ? FromUrl(new Uri(connectionString))
            : new NpgsqlConnectionStringBuilder(connectionString);

        // The app never uses Kerberos. Without this, Npgsql looks for libgssapi on every connection
        // and logs an error in containers that don't have it (like the ASP.NET Core image).
        if (!connectionString.Contains("GSS Encryption Mode", StringComparison.OrdinalIgnoreCase))
        {
            builder.GssEncryptionMode = GssEncryptionMode.Disable;
        }

        return builder.ConnectionString;
    }

    private static bool IsUrl(string value) =>
        value.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase)
        || value.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase);

    private static NpgsqlConnectionStringBuilder FromUrl(Uri url)
    {
        var userInfo = url.UserInfo.Split(':', 2);
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = url.Host,
            Port = url.IsDefaultPort || url.Port < 0 ? 5432 : url.Port,
            Database = Uri.UnescapeDataString(url.AbsolutePath.TrimStart('/')),
            Username = Uri.UnescapeDataString(userInfo[0]),
            Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : null,
        };

        // Query parameters like ?sslmode=require.
        foreach (var parameter in url.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var pair = parameter.Split('=', 2);
            if (pair[0].Equals("sslmode", StringComparison.OrdinalIgnoreCase) && pair.Length == 2)
            {
                builder.SslMode = Enum.Parse<SslMode>(Uri.UnescapeDataString(pair[1]), ignoreCase: true);
            }
        }

        return builder;
    }
}
