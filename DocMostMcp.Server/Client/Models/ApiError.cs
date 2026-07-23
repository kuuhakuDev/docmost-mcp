using System.Text.Json;
using DocMostMcp.Server.Json;

namespace DocMostMcp.Server.Client.Models;

/// <summary>
/// Represents an error response from the Docmost API.
/// </summary>
public sealed class ApiError
{
    /// <summary>HTTP status code of the error.</summary>
    public int StatusCode { get; set; }

    /// <summary>Human-readable error message. Can be a string or an array of strings.</summary>
    public object? Message { get; set; }

    /// <summary>Short error code or identifier.</summary>
    public string? Error { get; set; }

    /// <summary>
    /// Attempts to parse an <see cref="ApiError"/> from a <see cref="JsonElement"/>.
    /// Returns <c>null</c> if the element doesn't contain error fields.
    /// </summary>
    public static ApiError? FromJsonElement(JsonElement element)
    {
        if (!element.TryGetProperty("statusCode", out var statusCode) &&
            !element.TryGetProperty("error", out _))
        {
            return null;
        }

        var error = new ApiError
        {
            StatusCode = statusCode.ValueKind == JsonValueKind.Number ? statusCode.GetInt32() : 0,
            Error = element.TryGetProperty("error", out var errProp) && errProp.ValueKind == JsonValueKind.String
                ? errProp.GetString()
                : null,
        };

        if (element.TryGetProperty("message", out var msgProp))
        {
            error.Message = msgProp.ValueKind switch
            {
                JsonValueKind.String => msgProp.GetString(),
                JsonValueKind.Array => JsonSerializer.Deserialize(msgProp.GetRawText(), AppJsonSerializerContext.Default.StringArray),
                _ => msgProp.GetRawText(),
            };
        }

        return error;
    }
}
