using System.Text.Json;
using System.Text.Json.Serialization;

namespace MovieTracker.Blazor.Core.Api;

/// <summary>
/// The JSON of the API (camelCase, like the backend writes it). Source-generated, so that it keeps working
/// when the published app is trimmed.
/// </summary>
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(User))]
[JsonSerializable(typeof(Genre[]))]
[JsonSerializable(typeof(string[]))]
[JsonSerializable(typeof(Movie))]
[JsonSerializable(typeof(MovieRequest))]
[JsonSerializable(typeof(MovieSearchResponse))]
[JsonSerializable(typeof(PagedResponse<MovieSummary>))]
[JsonSerializable(typeof(DiaryEntry))]
[JsonSerializable(typeof(DiaryEntry[]))]
[JsonSerializable(typeof(DiaryEntryRequest))]
[JsonSerializable(typeof(PagedResponse<DiaryEntry>))]
[JsonSerializable(typeof(WatchlistItem))]
[JsonSerializable(typeof(WatchlistItemRequest))]
[JsonSerializable(typeof(WatchlistPage))]
[JsonSerializable(typeof(PagedResponse<ActivityLogEntry>))]
[JsonSerializable(typeof(ImportJob))]
[JsonSerializable(typeof(ProblemDetails))]
internal sealed partial class ApiJson : JsonSerializerContext;
