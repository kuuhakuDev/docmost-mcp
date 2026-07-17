using System.Text.Json;
using System.Text.Json.Serialization;

namespace DocMostMcp.Server.Client.Models;

/// <summary>
/// Represents a Docmost page as returned by the API.
/// </summary>
public sealed class Page
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("slugId")]
    public string SlugId { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("icon")]
    public string? Icon { get; set; }

    [JsonPropertyName("coverPhoto")]
    public string? CoverPhoto { get; set; }

    /// <summary>Page content. Can be a ProseMirror object, markdown string, or null depending on format.</summary>
    [JsonPropertyName("content")]
    public JsonElement? Content { get; set; }

    [JsonPropertyName("position")]
    public string? Position { get; set; }

    [JsonPropertyName("parentPageId")]
    public string? ParentPageId { get; set; }

    [JsonPropertyName("spaceId")]
    public string SpaceId { get; set; } = string.Empty;

    [JsonPropertyName("creatorId")]
    public string? CreatorId { get; set; }

    [JsonPropertyName("lastUpdatedById")]
    public string? LastUpdatedById { get; set; }

    [JsonPropertyName("isLocked")]
    public bool IsLocked { get; set; }

    [JsonPropertyName("contributorIds")]
    public string[]? ContributorIds { get; set; }

    [JsonPropertyName("workspaceId")]
    public string WorkspaceId { get; set; } = string.Empty;

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("updatedAt")]
    public DateTime UpdatedAt { get; set; }

    [JsonPropertyName("deletedAt")]
    public DateTime? DeletedAt { get; set; }

    // Extended properties from get_page / update response

    [JsonPropertyName("creator")]
    public PageUser? Creator { get; set; }

    [JsonPropertyName("lastUpdatedBy")]
    public PageUser? LastUpdatedBy { get; set; }

    [JsonPropertyName("contributors")]
    public PageUser[]? Contributors { get; set; }

    [JsonPropertyName("space")]
    public PageSpaceRef? Space { get; set; }

    [JsonPropertyName("hasChildren")]
    public bool HasChildren { get; set; }
}

/// <summary>
/// Minimal user reference embedded in page responses.
/// </summary>
public sealed class PageUser
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("avatarUrl")]
    public string? AvatarUrl { get; set; }
}

/// <summary>
/// Minimal space reference embedded in page responses.
/// </summary>
public sealed class PageSpaceRef
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("slug")]
    public string Slug { get; set; } = string.Empty;
}
