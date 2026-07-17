using Microsoft.Extensions.Options;

namespace DocMostMcp.Server.Configuration;

/// <summary>
/// Validates <see cref="DocmostOptions"/> at startup using <see cref="IValidateOptions{T}"/>.
/// </summary>
public sealed class DocmostOptionsValidator : IValidateOptions<DocmostOptions>
{
    public ValidateOptionsResult Validate(string? name, DocmostOptions options)
    {
        var failures = new List<string>();

        // Validate Url
        if (options.Url is null)
        {
            failures.Add("DOCMOST_URL is required but was not set or is empty.");
        }
        else if (!options.Url.IsAbsoluteUri || 
                 (options.Url.Scheme != "http" && options.Url.Scheme != "https"))
        {
            failures.Add(
                "DOCMOST_URL must be an absolute HTTP or HTTPS URL " +
                $"(e.g. http://localhost:3000). Current value: '{options.Url}'.");
        }

        // Validate Email
        if (string.IsNullOrWhiteSpace(options.Email))
        {
            failures.Add("DOCMOST_EMAIL is required but was not set or is empty.");
        }

        // Validate Password
        if (string.IsNullOrWhiteSpace(options.Password))
        {
            failures.Add("DOCMOST_PASSWORD is required but was not set or is empty.");
        }

        // Validate Port
        if (options.Port < 1 || options.Port > 65535)
        {
            failures.Add(
                $"DOCMOST_MCP_PORT must be between 1 and 65535. Current value: {options.Port}.");
        }

        // Validate Transport (only if explicitly set; auto-detected values are always valid)
        if (options.TransportExplicitlySet &&
            options.Transport != TransportMode.Stdio &&
            options.Transport != TransportMode.Http)
        {
            failures.Add(
                $"DOCMOST_MCP_TRANSPORT must be 'stdio' or 'http'. Current value: '{options.Transport}'.");
        }

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}
