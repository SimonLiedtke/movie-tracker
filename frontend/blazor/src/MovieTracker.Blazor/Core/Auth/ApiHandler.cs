namespace MovieTracker.Blazor.Core.Auth;

/// <summary>
/// For API requests: sends the CSRF header the backend requires on state-changing requests, and
/// tells <see cref="AuthService"/> when the session has expired.
/// </summary>
public sealed class ApiHandler(Func<AuthService> auth) : DelegatingHandler
{
    private static bool IsApi(Uri? url) => url is not null && url.AbsolutePath.StartsWith("/api/", StringComparison.Ordinal);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var isApi = IsApi(request.RequestUri);
        if (isApi)
        {
            request.Headers.TryAddWithoutValidation("X-CSRF", "1");
        }

        var response = await base.SendAsync(request, cancellationToken);

        // The auth endpoints answer 401 for "not signed in": that is for the auth service to handle.
        // Several requests can fail at once; only the first one ends the session (and remembers the page).
        if (isApi
            && response.StatusCode == System.Net.HttpStatusCode.Unauthorized
            && !request.RequestUri!.AbsolutePath.StartsWith("/api/auth/", StringComparison.Ordinal))
        {
            var service = auth();
            if (service.SignedIn)
            {
                service.SessionEnded(service.CurrentUrl());
            }
        }

        return response;
    }
}
