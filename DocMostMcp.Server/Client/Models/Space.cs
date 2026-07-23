using System.Text.Json.Serialization;

namespace DocMostMcp.Server.Client.Models;

/// <summary>
/// Represents a Docmost space returned by list and info endpoints.
/// </summary>
public sealed class Space
{
    /// <summary>Space unique identifier (UUID).</summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>Space display name.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>URL-friendly slug.</summary>
    [JsonPropertyName("slug")]
    public string Slug { get; set; } = string.Empty;

    /// <summary>Optional description.</summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>Optional logo URL.</summary>
    [JsonPropertyName("logo")]
    public string? Logo { get; set; }

    /// <summary>Default role assigned to new members.</summary>
    [JsonPropertyName("defaultRole")]
    public string DefaultRole { get; set; } = string.Empty;

    /// <summary>Visibility setting (e.g. "public", "private").</summary>
    [JsonPropertyName("visibility")]
    public string Visibility { get; set; } = string.Empty;

    /// <summary>Space settings as a JSON object.</summary>
    [JsonPropertyName("settings")]
    public System.Text.Json.JsonElement? Settings { get; set; }

    /// <summary>ID of the user who created the space.</summary>
    [JsonPropertyName("creatorId")]
    public string? CreatorId { get; set; }

    /// <summary>ID of the workspace the space belongs to.</summary>
    [JsonPropertyName("workspaceId")]
    public string WorkspaceId { get; set; } = string.Empty;

    /// <summary>Creation timestamp (ISO 8601).</summary>
    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }

    /// <summary>Last update timestamp (ISO 8601).</summary>
    [JsonPropertyName("updatedAt")]
    public DateTime UpdatedAt { get; set; }

    /// <summary>Number of members in the space (only in list responses).</summary>
    [JsonPropertyName("memberCount")]
    public int MemberCount { get; set; }

    /// <summary>Current user's membership details (only in space-info responses).</summary>
    [JsonPropertyName("membership")]
    public SpaceMembership? Membership { get; set; }
}

/// <summary>
/// Current user's membership within a space.
/// </summary>
public sealed class SpaceMembership
{
    /// <summary>User ID.</summary>
    [JsonPropertyName("userId")]
    public string UserId { get; set; } = string.Empty;

    /// <summary>User's role in the space.</summary>
    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    /// <summary>List of permission entries.</summary>
    [JsonPropertyName("permissions")]
    public SpacePermission[]? Permissions { get; set; }
}

/// <summary>
/// A permission entry within a space membership.
/// </summary>
public sealed class SpacePermission
{
    /// <summary>Action the permission grants (e.g. "create", "read").</summary>
    [JsonPropertyName("action")]
    public string Action { get; set; } = string.Empty;

    /// <summary>Subject the permission applies to (e.g. "Page").</summary>
    [JsonPropertyName("subject")]
    public string Subject { get; set; } = string.Empty;
}
