namespace DocMostMcp.Server.Client;

/// <summary>
/// Thrown when the Docmost authentication fails (invalid credentials or unrecoverable 401).
/// </summary>
public sealed class DocmostAuthException : Exception
{
    private const string DefaultMessage =
        "Failed to authenticate with Docmost. " +
        "Please verify that DOCMOST_EMAIL and DOCMOST_PASSWORD environment variables are correct.";

    /// <summary>
    /// Initializes a new instance with the default error message.
    /// </summary>
    public DocmostAuthException()
        : base(DefaultMessage)
    {
    }

    /// <summary>
    /// Initializes a new instance with a custom error message.
    /// </summary>
    public DocmostAuthException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance with a custom error message and inner exception.
    /// </summary>
    public DocmostAuthException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
