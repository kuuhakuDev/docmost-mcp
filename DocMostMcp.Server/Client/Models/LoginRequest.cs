using System.Text.Json.Serialization;

namespace DocMostMcp.Server.Client.Models;

/// <summary>
/// Request body for POST /api/auth/login.
/// </summary>
public sealed record LoginRequest(
    [property: JsonPropertyName("email")] string Email,
    [property: JsonPropertyName("password")] string Password
);
