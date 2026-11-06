using System.Reflection;
using MovieTracker.Application.Movies;
using MovieTracker.Domain.Movies;

namespace MovieTracker.Api.Tests;

/// <summary>
/// Keeps the layers apart: Domain, then Application (use cases and the ports they need), then Infrastructure (database,
/// TMDB; implements the ports). The Api project only maps HTTP to the use cases, so only Infrastructure accesses the database.
/// </summary>
public sealed class ArchitectureTests
{
    private static readonly Assembly Domain = typeof(Movie).Assembly;
    private static readonly Assembly Application = typeof(MovieService).Assembly;
    private static readonly Assembly Api = typeof(Program).Assembly;

    [Fact]
    public void The_domain_depends_on_nothing_but_the_base_library()
    {
        Assert.Empty(References(Domain));
    }

    [Fact]
    public void The_application_depends_on_the_domain_and_abstractions_only()
    {
        // No EF Core, no ASP.NET Core, no Infrastructure: the use cases only know ports.
        string[] allowed =
        [
            "MovieTracker.Domain",
            "Microsoft.Extensions.DependencyInjection.Abstractions",
            "Microsoft.Extensions.Logging.Abstractions",
        ];

        Assert.Empty(References(Application).Except(allowed));
    }

    [Fact]
    public void The_api_does_not_use_the_database()
    {
        string[] database = ["Microsoft.EntityFrameworkCore", "Npgsql", "EFCore.", "Microsoft.AspNetCore.DataProtection.EntityFrameworkCore"];

        var violations = References(Api).Where(name => database.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal)));

        Assert.Empty(violations);
    }

    [Fact]
    public void The_api_does_not_take_Infrastructure_types_in_its_classes()
    {
        // Controllers, the import worker and the rest get use cases and ports, never the Infrastructure's own types.
        // Program is the composition root: it wires Infrastructure up (e.g. with its TmdbOptions) and may know it.
        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        var violations = Api.GetTypes()
            .Where(type => type != typeof(Program) && type.DeclaringType != typeof(Program))
            .SelectMany(type =>
                type.GetConstructors(all).SelectMany(constructor => constructor.GetParameters().Select(parameter => parameter.ParameterType))
                    .Concat(type.GetFields(all).Select(field => field.FieldType))
                    .Concat(type.GetProperties(all).Select(property => property.PropertyType))
                    .Concat(type.GetMethods(all).SelectMany(method => method.GetParameters().Select(parameter => parameter.ParameterType).Append(method.ReturnType)))
                    .Where(dependency => dependency.Namespace?.StartsWith("MovieTracker.Infrastructure", StringComparison.Ordinal) == true)
                    .Select(dependency => $"{type.FullName} uses {dependency.FullName}"))
            .Distinct();

        Assert.Empty(violations);
    }

    /// <summary>The assemblies a project depends on, other than the base library.</summary>
    private static IEnumerable<string> References(Assembly assembly) =>
        assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name!)
            .Where(name => name != "netstandard" && name != "mscorlib" && !name.StartsWith("System", StringComparison.Ordinal))
            .Order();
}
