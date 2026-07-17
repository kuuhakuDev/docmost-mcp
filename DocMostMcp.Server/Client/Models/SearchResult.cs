using System.Text.Json.Serialization;

namespace DocMostMcp.Server.Client.Models;

/// <summary>
/// A single search result from full-text page search.
/// </summary>
public sealed class SearchResult
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("icon")]
    public string? Icon { get; set; }

    [JsonPropertyName("parentPageId")]
    public string? ParentPageId { get; set; }

    [JsonPropertyName("creatorId")]
    public string? CreatorId { get; set; }

    [JsonPropertyName("rank")]
    public double Rank { get; set; }

    [JsonPropertyName("highlight")]
    public string Highlight { get; set; } = string.Empty;

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("updatedAt")]
    public DateTime UpdatedAt { get; set; }

    [JsonPropertyName("space")]
    public SearchResultSpace? Space { get; set; }
}

/// <summary>
/// Minimal space reference within a search result.
/// </summary>
public sealed class SearchResultSpace
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("slug")]
    public string Slug { get; set; } = string.Empty;
}
