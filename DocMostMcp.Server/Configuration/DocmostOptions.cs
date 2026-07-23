namespace DocMostMcp.Server.Configuration;

/// <summary>
/// Strongly-typed options read from environment variables for the Docmost MCP server.
/// </summary>
public sealed class DocmostOptions
{
    public const string SectionName = "Docmost";

    /// <summary>
    /// Base URL of the Docmost instance (e.g. http://localhost:3000).
    /// Maps from <c>DOCMOST_URL</c>.
    /// </summary>
    public Uri? Url { get; set; }

    /// <summary>
    /// Email address for Docmost authentication.
    /// Maps from <c>DOCMOST_EMAIL</c>.
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// Password for Docmost authentication.
    /// Maps from <c>DOCMOST_PASSWORD</c>.
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// Port number for HTTP transport. Defaults to 3001.
    /// Maps from <c>DOCMOST_MCP_PORT</c>.
    /// </summary>
    public int Port { get; set; } = 3001;

    /// <summary>
    /// Transport mode: <c>Stdio</c> or <c>Http</c>.
    /// Maps from <c>DOCMOST_MCP_TRANSPORT</c>.
    /// When not set, auto-detected based on whether stdin is redirected.
    /// </summary>
    public TransportMode Transport { get; set; }

    /// <summary>
    /// Whether the transport variable was explicitly set by the user (vs auto-detected).
    /// </summary>
    public bool TransportExplicitlySet { get; set; }
}

/// <summary>
/// Supported MCP transport modes.
/// </summary>
public enum TransportMode
{
    /// <summary>Standard I/O transport (local MCP, used by IDEs).</summary>
    Stdio = 0,

    /// <summary>HTTP Streamable transport (remote MCP).</summary>
    Http = 1,
}
