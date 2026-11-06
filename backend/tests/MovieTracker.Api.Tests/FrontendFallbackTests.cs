using System.Net;
using MovieTracker.Api.Tests.Infrastructure;

namespace MovieTracker.Api.Tests;

/// <summary>The API also serves the Angular app; unknown API URLs must not be answered with it.</summary>
public sealed class FrontendFallbackTests(ApiFactory factory) : ApiTestBase(factory)
{
    [Theory]
    [InlineData("/api/does-not-exist")]
    [InlineData("/api/movies/search/extra")]
    public async Task Unknown_api_urls_return_404_problem_details(string url)
    {
        var response = await Client.GetAsync(url, Ct);

        await ReadProblemAsync(response, HttpStatusCode.NotFound);
    }
}
