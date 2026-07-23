using System.Text.Json.Serialization;

namespace DocMostMcp.Server.Tools;

/// <summary>
/// Standard error envelope returned by all MCP tools on failure.
/// Serialized with the same keys as the previous anonymous-type envelope
/// to maintain backward compatibility.
/// </summary>
public sealed record ErrorResponse(
    [property: JsonPropertyName("ok")]
    bool Ok,

    [property: JsonPropertyName("statusCode")]
    int StatusCode,

    [property: JsonPropertyName("error")]
    string Error,

    [property: JsonPropertyName("details")]
    object? Details = null
);
