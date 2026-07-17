using System.Text.Json.Serialization;

namespace DocMostMcp.Server.Client.Models;

/// <summary>
/// Cursor-based pagination metadata returned by list endpoints.
/// </summary>
public sealed class PaginationMeta
{
    /// <summary>Maximum number of items per page.</summary>
    [JsonPropertyName("limit")]
    public int Limit { get; set; }

    /// <summary>Whether there are more items in the forward direction.</summary>
    [JsonPropertyName("hasNextPage")]
    public bool HasNextPage { get; set; }

    /// <summary>Whether there are more items in the backward direction.</summary>
    [JsonPropertyName("hasPrevPage")]
    public bool HasPrevPage { get; set; }

    /// <summary>Opaque cursor to fetch the next page (base64).</summary>
    [JsonPropertyName("nextCursor")]
    public string? NextCursor { get; set; }

    /// <summary>Opaque cursor to fetch the previous page (base64).</summary>
    [JsonPropertyName("prevCursor")]
    public string? PrevCursor { get; set; }
}
