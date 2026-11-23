using System.Net;
using System.Text.Json;

namespace MovieTracker.Blazor.Core.Api;

/// <summary>RFC 9457 problem details, as returned by the backend for errors.</summary>
internal sealed record ProblemDetails(
    string? Title,
    string? Detail,
    int? Status,
    Dictionary<string, string[]>? Errors);

/// <summary>An error from an API call, in a form that can be shown to the user.</summary>
/// <param name="Status">The HTTP status; 0 if the request did not get an answer.</param>
/// <param name="Details">Validation messages, one per line.</param>
public sealed record ApiError(int Status, string Message, IReadOnlyList<string> Details)
{
    public static ApiError Unexpected { get; } = new(0, "An unexpected error occurred.", []);

    /// <summary>Turns any exception from an API call into a message that can be shown to the user.</summary>
    public static ApiError From(Exception exception) => exception switch
    {
        ApiException api => api.Error,
        // No response: the request did not get through.
        HttpRequestException => new ApiError(
            0, "The API cannot be reached. Is the backend running on http://localhost:5080?", []),
        _ => Unexpected,
    };

    internal static async Task<ApiError> FromResponseAsync(HttpResponseMessage response)
    {
        var status = (int)response.StatusCode;
        ProblemDetails? problem = null;
        try
        {
            problem = await response.Content.ReadFromJsonAsync(ApiJson.Default.ProblemDetails);
        }
        catch (Exception e) when (e is JsonException or NotSupportedException or InvalidOperationException)
        {
            // Not problem details (e.g. HTML from a proxy): only the status is known.
        }

        var details = problem?.Errors?.Values.SelectMany(messages => messages).ToList() ?? [];
        var message = problem?.Detail ?? problem?.Title ?? $"Request failed (HTTP {status}).";
        return new ApiError(status, message, details);
    }

    public bool IsNotFound => Status == (int)HttpStatusCode.NotFound;
}

/// <summary>Thrown by the API clients when the server answers with an error.</summary>
public sealed class ApiException(ApiError error) : Exception(error.Message)
{
    public ApiError Error { get; } = error;
}
