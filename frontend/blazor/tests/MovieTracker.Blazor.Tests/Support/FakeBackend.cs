using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace MovieTracker.Blazor.Tests.Support;

/// <summary>What the app sent.</summary>
public sealed record SentRequest(HttpMethod Method, string Path, string Query, string? Body, string? ContentType, bool HasCsrfHeader)
{
    public string PathAndQuery => Path + Query;

    public T? Json<T>() => JsonSerializer.Deserialize<T>(Body!, JsonSerializerOptions.Web);
}

/// <summary>
/// Stands in for the network: answers the requests of the app from a table of routes (the path decides, the
/// query is recorded) and records what was sent. Requests no route is registered for are answered with 404.
/// </summary>
public sealed class FakeBackend : HttpMessageHandler
{
    private readonly List<(HttpMethod Method, string Path, Func<SentRequest, HttpResponseMessage> Respond)> routes = [];
    private readonly List<SentRequest> requests = [];

    public IReadOnlyList<SentRequest> Requests => requests;

    public IEnumerable<SentRequest> Sent(HttpMethod method, string path) =>
        requests.Where(request => request.Method == method && request.Path == path);

    /// <summary>The last request to the path: asserts that there is one.</summary>
    public SentRequest Last(HttpMethod method, string path) => Sent(method, path).Last();

    public FakeBackend On(HttpMethod method, string path, Func<SentRequest, HttpResponseMessage> respond)
    {
        routes.RemoveAll(route => route.Method == method && route.Path == path);
        routes.Add((method, path, respond));
        return this;
    }

    public FakeBackend Json(HttpMethod method, string path, object? body, HttpStatusCode status = HttpStatusCode.OK) =>
        On(method, path, _ => JsonResponse(body, status));

    public FakeBackend Get(string path, object? body) => Json(HttpMethod.Get, path, body);

    public FakeBackend NoContent(HttpMethod method, string path) =>
        On(method, path, _ => new HttpResponseMessage(HttpStatusCode.NoContent));

    /// <summary>An RFC 9457 problem, as the backend answers errors.</summary>
    public FakeBackend Problem(HttpMethod method, string path, HttpStatusCode status, string detail, Dictionary<string, string[]>? errors = null) =>
        On(method, path, _ => Problem(status, detail, errors));

    public static HttpResponseMessage JsonResponse(object? body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status)
        {
            Content = new StringContent(JsonSerializer.Serialize(body, JsonSerializerOptions.Web), Encoding.UTF8, "application/json"),
        };

    public static HttpResponseMessage Problem(HttpStatusCode status, string detail, Dictionary<string, string[]>? errors = null) =>
        new(status)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new { title = "Problem", status = (int)status, detail, errors }, JsonSerializerOptions.Web),
                Encoding.UTF8,
                new MediaTypeHeaderValue("application/problem+json")),
        };

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var contentType = request.Content?.Headers.ContentType?.MediaType;
        // The body of an upload is kept as text too (enough to see the field and file name).
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        var sent = new SentRequest(
            request.Method,
            request.RequestUri!.AbsolutePath,
            request.RequestUri.Query,
            body,
            contentType,
            request.Headers.TryGetValues("X-CSRF", out var csrf) && csrf.Contains("1"));
        requests.Add(sent);

        var route = routes.FirstOrDefault(r => r.Method == request.Method && r.Path == sent.Path);
        return route.Respond is null
            ? Problem(HttpStatusCode.NotFound, $"No route for {request.Method} {sent.Path}")
            : route.Respond(sent);
    }
}
