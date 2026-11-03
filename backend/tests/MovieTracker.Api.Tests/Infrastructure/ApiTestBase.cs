using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using MovieTracker.Api.Contracts;

namespace MovieTracker.Api.Tests.Infrastructure;

public abstract class ApiTestBase(ApiFactory factory) : IAsyncLifetime
{
    protected const int Horror = 27;
    protected const int ScienceFiction = 878;
    protected const int Drama = 18;

    protected ApiFactory Factory { get; } = factory;

    protected HttpClient Client { get; } = factory.CreateClient();

    protected static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await Factory.ResetAsync();

    public ValueTask DisposeAsync()
    {
        Client.Dispose();
        return ValueTask.CompletedTask;
    }

    protected async Task<MovieResponse> CreateMovieAsync(
        string title = "Alien",
        string? releaseDate = "1979-05-25",
        int? tmdbId = null,
        string? originalTitle = null,
        int[]? genreIds = null)
    {
        var response = await Client.PostAsJsonAsync("/api/movies", new
        {
            title,
            originalTitle,
            releaseDate,
            runtimeMinutes = 117,
            tmdbId,
            genreIds = genreIds ?? [Horror, ScienceFiction],
        }, Ct);
        return await ReadAsync<MovieResponse>(response, HttpStatusCode.Created);
    }

    protected async Task<DiaryEntryResponse> CreateDiaryEntryAsync(
        Guid movieId, string watchedOn = "2026-10-31", int? rating = 8, string? review = null)
    {
        var response = await Client.PostAsJsonAsync("/api/diary-entries", new
        {
            movieId,
            watchedOn,
            rating,
            review,
        }, Ct);
        return await ReadAsync<DiaryEntryResponse>(response, HttpStatusCode.Created);
    }

    protected async Task<T> GetAsync<T>(string url)
    {
        var response = await Client.GetAsync(url, Ct);
        return await ReadAsync<T>(response, HttpStatusCode.OK);
    }

    /// <summary>Asserts the status code and reads the body, showing the body if the status is unexpected.</summary>
    protected static async Task<T> ReadAsync<T>(HttpResponseMessage response, HttpStatusCode expectedStatus)
    {
        if (response.StatusCode != expectedStatus)
        {
            var body = await response.Content.ReadAsStringAsync(Ct);
            Assert.Fail($"Expected {(int)expectedStatus} but got {(int)response.StatusCode}: {body}");
        }

        var result = await response.Content.ReadFromJsonAsync<T>(Ct);
        Assert.NotNull(result);
        return result;
    }

    protected static async Task<ValidationProblemDetails> ReadValidationProblemAsync(HttpResponseMessage response)
    {
        var problem = await ReadAsync<ValidationProblemDetails>(response, HttpStatusCode.BadRequest);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        return problem;
    }

    protected static async Task<ProblemDetails> ReadProblemAsync(HttpResponseMessage response, HttpStatusCode expectedStatus)
    {
        var problem = await ReadAsync<ProblemDetails>(response, expectedStatus);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal((int)expectedStatus, problem.Status);
        return problem;
    }
}
