namespace DocMostMcp.Server.Client.Models;

/// <summary>
/// Request body for POST /api/auth/login.
/// </summary>
public sealed record LoginRequest(string Email, string Password);
