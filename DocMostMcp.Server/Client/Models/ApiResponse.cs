using System.Text.Json;

namespace DocMostMcp.Server.Client.Models;

/// <summary>
/// Generic envelope returned by all Docmost API endpoints.
/// </summary>
/// <typeparam name="T">Type of the <see cref="Data"/> payload.</typeparam>
public sealed class ApiResponse<T>
{
    /// <summary>Indicates whether the API call succeeded.</summary>
    public bool Success { get; set; }

    /// <summary>HTTP status code.</summary>
    public int Status { get; set; }

    /// <summary>Response payload, if any.</summary>
    public T? Data { get; set; }

    /// <summary>
    /// Parses an <see cref="ApiResponse{T}"/> from a <see cref="JsonElement"/>.
    /// </summary>
    public static ApiResponse<T> FromJsonElement(JsonElement element, JsonSerializerOptions? options = null)
    {
        options ??= JsonSerializerOptions.Default;

        var response = new ApiResponse<T>
        {
            Success = element.TryGetProperty("success", out var success)
                && success.ValueKind == JsonValueKind.True,
            Status = element.TryGetProperty("status", out var status)
                ? status.GetInt32()
                : 0,
        };

        if (element.TryGetProperty("data", out var data) && data.ValueKind != JsonValueKind.Null)
        {
            response.Data = JsonSerializer.Deserialize<T>(data.GetRawText(), options);
        }

        return response;
    }
}
