using System.Text.Json.Serialization.Metadata;

namespace MovieTracker.Blazor.Core.Api;

/// <summary>
/// The HTTP client of the app: JSON in and JSON out. A request that fails is thrown as <see cref="ApiException"/>
/// (the server answered with an error) or <see cref="HttpRequestException"/> (no answer).
/// </summary>
public sealed class ApiClient(HttpClient http)
{
    public Task<T> GetAsync<T>(string url, JsonTypeInfo<T> type) => SendAsync(HttpMethod.Get, url, null, type);

    public Task<T> PostAsync<T>(string url, JsonTypeInfo<T> type) => SendAsync(HttpMethod.Post, url, null, type);

    public Task<T> PostAsync<T>(string url, HttpContent content, JsonTypeInfo<T> type) =>
        SendAsync(HttpMethod.Post, url, content, type);

    public Task<T> PostJsonAsync<TBody, T>(string url, TBody body, JsonTypeInfo<TBody> bodyType, JsonTypeInfo<T> type) =>
        SendAsync(HttpMethod.Post, url, JsonContent.Create(body, bodyType), type);

    public Task<T> PutJsonAsync<TBody, T>(string url, TBody body, JsonTypeInfo<TBody> bodyType, JsonTypeInfo<T> type) =>
        SendAsync(HttpMethod.Put, url, JsonContent.Create(body, bodyType), type);

    public Task<T> PutAsync<T>(string url, HttpContent content, JsonTypeInfo<T> type) =>
        SendAsync(HttpMethod.Put, url, content, type);

    /// <summary>A request whose answer has no body (204).</summary>
    public async Task SendAsync(HttpMethod method, string url)
    {
        using var request = new HttpRequestMessage(method, url);
        using var response = await SendCoreAsync(request);
    }

    private async Task<T> SendAsync<T>(HttpMethod method, string url, HttpContent? content, JsonTypeInfo<T> type)
    {
        using var request = new HttpRequestMessage(method, url) { Content = content };
        using var response = await SendCoreAsync(request);
        return await response.Content.ReadFromJsonAsync(type)
            ?? throw new ApiException(new ApiError(0, "The API sent an empty answer.", []));
    }

    private async Task<HttpResponseMessage> SendCoreAsync(HttpRequestMessage request)
    {
        var response = await http.SendAsync(request);
        if (response.IsSuccessStatusCode)
        {
            return response;
        }

        using (response)
        {
            throw new ApiException(await ApiError.FromResponseAsync(response));
        }
    }
}
