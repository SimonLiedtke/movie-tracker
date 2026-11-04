namespace MovieTracker.Api.Auth;

/// <summary>
/// Requires the header <c>X-CSRF: 1</c> on every state-changing request. Browsers only send custom
/// headers cross-origin after a CORS preflight, which this API never allows, so a foreign site cannot
/// make the browser send such a request with the user's session cookie.
/// SameSite=Lax on the cookie already blocks most cases; this also covers other sites on the same "site",
/// e.g. another app on localhost.
/// </summary>
public sealed class CsrfHeaderMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-CSRF";

    public async Task InvokeAsync(HttpContext context, IProblemDetailsService problemDetails)
    {
        var request = context.Request;
        var isSafeMethod = HttpMethods.IsGet(request.Method)
            || HttpMethods.IsHead(request.Method)
            || HttpMethods.IsOptions(request.Method)
            || HttpMethods.IsTrace(request.Method);

        if (isSafeMethod || request.Headers[HeaderName] == "1")
        {
            await next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await problemDetails.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails =
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Missing CSRF header",
                Detail = $"State-changing requests must send the header '{HeaderName}: 1'.",
            },
        });
    }
}
