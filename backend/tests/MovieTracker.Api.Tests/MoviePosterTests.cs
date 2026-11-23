using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MovieTracker.Api.Contracts;
using MovieTracker.Api.Tests.Infrastructure;
using MovieTracker.Domain.Movies;
using MovieTracker.Infrastructure.Persistence;
using MovieTracker.TestSupport;

namespace MovieTracker.Api.Tests;

public sealed class MoviePosterTests(ApiFactory factory) : ApiTestBase(factory)
{
    private static readonly byte[] Jpeg = TestImages.Jpeg();
    private static readonly byte[] Png = TestImages.Png();

    private static MultipartFormDataContent Upload(byte[] data, string fileName = "poster.jpg", string contentType = "image/jpeg")
    {
        var file = new ByteArrayContent(data);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        return new MultipartFormDataContent { { file, "file", fileName } };
    }

    private async Task<MovieResponse> UploadPosterAsync(Guid movieId, byte[] data, HttpClient? client = null)
    {
        using var content = Upload(data);
        var response = await (client ?? Client).PutAsync($"/api/movies/{movieId}/poster", content, Ct);
        return await ReadAsync<MovieResponse>(response, HttpStatusCode.OK);
    }

    [Fact]
    public async Task Upload_sets_the_poster_of_the_movie_everywhere_it_is_shown()
    {
        var movie = await CreateMovieAsync();
        var url = $"/api/movies/{movie.Id}/poster";

        var updated = await UploadPosterAsync(movie.Id, Jpeg);

        Assert.Equal(url, updated.PosterUrl);
        Assert.Equal(url, (await GetAsync<MovieResponse>($"/api/movies/{movie.Id}")).PosterUrl);
        Assert.Equal(url, Assert.Single((await GetAsync<PagedResponse<MovieSummaryResponse>>("/api/movies")).Items).PosterUrl);
        var search = await GetAsync<MovieSearchResponse>("/api/movies/search?query=alien");
        // Next to the TMDB results (the fake TMDB knows an Alien as well).
        Assert.Equal(url, Assert.Single(search.Items, item => item.MovieId == movie.Id).PosterUrl);
    }

    [Fact]
    public async Task The_poster_is_served_with_the_type_of_its_data()
    {
        var movie = await CreateMovieAsync();
        await UploadPosterAsync(movie.Id, Png);

        var response = await Client.GetAsync($"/api/movies/{movie.Id}/poster", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/png", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(Png, await response.Content.ReadAsByteArrayAsync(Ct));
        Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
        Assert.True(response.Headers.CacheControl?.Private);
        Assert.True(response.Headers.CacheControl?.NoCache);
    }

    [Fact]
    public async Task The_poster_is_not_sent_again_if_the_browser_has_it()
    {
        var movie = await CreateMovieAsync();
        await UploadPosterAsync(movie.Id, Jpeg);
        var first = await Client.GetAsync($"/api/movies/{movie.Id}/poster", Ct);

        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/movies/{movie.Id}/poster");
        request.Headers.IfNoneMatch.Add(first.Headers.ETag!);
        var second = await Client.SendAsync(request, Ct);

        Assert.Equal(HttpStatusCode.NotModified, second.StatusCode);
    }

    [Fact]
    public async Task Upload_replaces_the_previous_poster()
    {
        var movie = await CreateMovieAsync();
        await UploadPosterAsync(movie.Id, Jpeg);
        var firstEtag = (await Client.GetAsync($"/api/movies/{movie.Id}/poster", Ct)).Headers.ETag;
        Factory.Clock.Advance(TimeSpan.FromMinutes(1));

        await UploadPosterAsync(movie.Id, Png);

        var response = await Client.GetAsync($"/api/movies/{movie.Id}/poster", Ct);
        Assert.Equal("image/png", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(Png, await response.Content.ReadAsByteArrayAsync(Ct));
        Assert.NotEqual(firstEtag, response.Headers.ETag);
    }

    [Fact]
    public async Task Upload_replaces_a_poster_url()
    {
        var created = await Client.PostAsJsonAsync("/api/movies", new
        {
            title = "Alien",
            posterUrl = "https://example.com/alien.jpg",
            genreIds = Array.Empty<int>(),
        }, Ct);
        var movie = await ReadAsync<MovieResponse>(created, HttpStatusCode.Created);
        Assert.Equal("https://example.com/alien.jpg", movie.PosterUrl);

        var updated = await UploadPosterAsync(movie.Id, Jpeg);

        Assert.Equal($"/api/movies/{movie.Id}/poster", updated.PosterUrl);
    }

    [Theory]
    [InlineData("poster.svg", "image/svg+xml", "<svg xmlns=\"http://www.w3.org/2000/svg\"/>")]
    [InlineData("poster.jpg", "image/jpeg", "this is text, whatever the client says")]
    [InlineData("poster.jpg", "image/jpeg", "")]
    public async Task Upload_rejects_files_that_are_not_jpeg_png_or_webp_images(string fileName, string contentType, string text)
    {
        var movie = await CreateMovieAsync();
        using var content = Upload(System.Text.Encoding.UTF8.GetBytes(text), fileName, contentType);

        var response = await Client.PutAsync($"/api/movies/{movie.Id}/poster", content, Ct);

        var problem = await ReadValidationProblemAsync(response);
        Assert.Contains("file", problem.Errors.Keys);
        Assert.Null((await GetAsync<MovieResponse>($"/api/movies/{movie.Id}")).PosterUrl);
    }

    [Fact]
    public async Task Upload_rejects_a_file_that_is_too_big()
    {
        var movie = await CreateMovieAsync();
        var data = new byte[MoviePoster.MaxBytes + 1];
        Jpeg.CopyTo(data, 0);
        using var content = Upload(data);

        var response = await Client.PutAsync($"/api/movies/{movie.Id}/poster", content, Ct);

        var problem = await ReadValidationProblemAsync(response);
        Assert.Contains("file", problem.Errors.Keys);
    }

    [Fact]
    public async Task Upload_rejects_a_request_without_a_file()
    {
        var movie = await CreateMovieAsync();
        using var content = new MultipartFormDataContent { { new StringContent("x"), "other" } };

        var response = await Client.PutAsync($"/api/movies/{movie.Id}/poster", content, Ct);

        await ReadValidationProblemAsync(response);
    }

    [Fact]
    public async Task Upload_is_refused_for_a_tmdb_movie()
    {
        var movie = await ImportTmdbMovieAsync();
        using var content = Upload(Jpeg);

        var response = await Client.PutAsync($"/api/movies/{movie.Id}/poster", content, Ct);

        await ReadProblemAsync(response, HttpStatusCode.Forbidden);
        Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync($"/api/movies/{movie.Id}/poster", Ct)).StatusCode);
    }

    [Fact]
    public async Task Upload_returns_404_for_an_unknown_movie()
    {
        using var content = Upload(Jpeg);

        var response = await Client.PutAsync($"/api/movies/{Guid.NewGuid()}/poster", content, Ct);

        await ReadProblemAsync(response, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Other_users_cannot_see_or_replace_the_poster()
    {
        var movie = await CreateMovieAsync();
        await UploadPosterAsync(movie.Id, Jpeg);
        using var content = Upload(Png);

        var get = await Bob.GetAsync($"/api/movies/{movie.Id}/poster", Ct);
        var put = await Bob.PutAsync($"/api/movies/{movie.Id}/poster", content, Ct);

        await ReadProblemAsync(get, HttpStatusCode.NotFound);
        await ReadProblemAsync(put, HttpStatusCode.NotFound);
        Assert.Equal(Jpeg, await (await Client.GetAsync($"/api/movies/{movie.Id}/poster", Ct)).Content.ReadAsByteArrayAsync(Ct));
    }

    [Fact]
    public async Task The_poster_needs_a_signed_in_user()
    {
        var movie = await CreateMovieAsync();
        await UploadPosterAsync(movie.Id, Jpeg);

        var response = await Anonymous.GetAsync($"/api/movies/{movie.Id}/poster", Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task A_movie_without_an_uploaded_poster_has_no_poster_image()
    {
        var movie = await CreateMovieAsync();

        var response = await Client.GetAsync($"/api/movies/{movie.Id}/poster", Ct);

        await ReadProblemAsync(response, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Saving_the_movie_with_its_poster_url_keeps_the_uploaded_poster()
    {
        var movie = await CreateMovieAsync();
        var uploaded = await UploadPosterAsync(movie.Id, Jpeg);

        var response = await Client.PutAsJsonAsync($"/api/movies/{movie.Id}", new
        {
            title = "Aliens",
            posterUrl = uploaded.PosterUrl,
            genreIds = Array.Empty<int>(),
        }, Ct);

        var updated = await ReadAsync<MovieResponse>(response, HttpStatusCode.OK);
        Assert.Equal(uploaded.PosterUrl, updated.PosterUrl);
        Assert.Equal(Jpeg, await (await Client.GetAsync($"/api/movies/{movie.Id}/poster", Ct)).Content.ReadAsByteArrayAsync(Ct));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("https://example.com/other.jpg")]
    public async Task Saving_the_movie_with_another_poster_url_removes_the_uploaded_poster(string? posterUrl)
    {
        var movie = await CreateMovieAsync();
        await UploadPosterAsync(movie.Id, Jpeg);

        var response = await Client.PutAsJsonAsync($"/api/movies/{movie.Id}", new
        {
            title = "Alien",
            posterUrl,
            genreIds = Array.Empty<int>(),
        }, Ct);

        var updated = await ReadAsync<MovieResponse>(response, HttpStatusCode.OK);
        Assert.Equal(posterUrl, updated.PosterUrl);
        Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync($"/api/movies/{movie.Id}/poster", Ct)).StatusCode);
    }

    [Fact]
    public async Task Saving_a_movie_rejects_the_uploaded_poster_url_of_another_movie()
    {
        var other = await CreateMovieAsync("Other");
        await UploadPosterAsync(other.Id, Jpeg);
        var movie = await CreateMovieAsync();

        var response = await Client.PutAsJsonAsync($"/api/movies/{movie.Id}", new
        {
            title = "Alien",
            posterUrl = $"/api/movies/{other.Id}/poster",
            genreIds = Array.Empty<int>(),
        }, Ct);

        var problem = await ReadValidationProblemAsync(response);
        Assert.Contains("posterUrl", problem.Errors.Keys);
    }

    [Theory]
    [InlineData("/elsewhere/poster.jpg")]
    [InlineData("/api/movies/ with space")]
    public async Task Create_rejects_relative_poster_urls(string posterUrl)
    {
        var response = await Client.PostAsJsonAsync("/api/movies", new { title = "Alien", posterUrl, genreIds = Array.Empty<int>() }, Ct);

        var problem = await ReadValidationProblemAsync(response);
        Assert.Contains("posterUrl", problem.Errors.Keys);
    }

    [Fact]
    public async Task Create_rejects_the_url_of_an_upload_that_cannot_exist_yet()
    {
        var response = await Client.PostAsJsonAsync("/api/movies", new
        {
            title = "Alien",
            posterUrl = $"/api/movies/{Guid.NewGuid()}/poster",
            genreIds = Array.Empty<int>(),
        }, Ct);

        var problem = await ReadValidationProblemAsync(response);
        Assert.Contains("posterUrl", problem.Errors.Keys);
    }

    [Fact]
    public async Task Deleting_the_movie_deletes_its_poster()
    {
        var movie = await CreateMovieAsync();
        await UploadPosterAsync(movie.Id, Jpeg);

        var response = await Client.DeleteAsync($"/api/movies/{movie.Id}", Ct);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MovieTrackerDbContext>();
        Assert.False(await db.MoviePosters.AnyAsync(poster => poster.MovieId == movie.Id, Ct));
    }

    [Fact]
    public async Task Uploading_a_poster_is_in_the_activity_log()
    {
        var movie = await CreateMovieAsync();
        Factory.Clock.Advance(TimeSpan.FromMinutes(1));

        await UploadPosterAsync(movie.Id, Jpeg);

        var log = await GetAsync<PagedResponse<ActivityLogEntryResponse>>("/api/activity-log");
        Assert.Equal(ActivityLogType.MovieUpdated, log.Items[0].Type);
        Assert.Equal(movie.Id, log.Items[0].MovieId);
    }
}
