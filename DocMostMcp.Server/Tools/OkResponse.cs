using System.Text.Json.Serialization;

namespace DocMostMcp.Server.Tools;

/// <summary>
/// Standard success envelope returned by all MCP tools.
/// Serialized with the same keys as the previous anonymous-type envelope
/// to maintain backward compatibility.
/// </summary>
public sealed record OkResponse<T>(
    [property: JsonPropertyName("ok")]
    bool Ok,

    [property: JsonPropertyName("statusCode")]
    int StatusCode,

    [property: JsonPropertyName("data")]
    T? Data
);
