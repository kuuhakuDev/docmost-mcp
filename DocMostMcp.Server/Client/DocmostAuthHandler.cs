using System.Net;
using DocMostMcp.Server.Configuration;
using Microsoft.Extensions.Options;

namespace DocMostMcp.Server.Client;

/// <summary>
/// <see cref="DelegatingHandler"/> that injects the Docmost session cookie into every request
/// and transparently retries once on 401 by triggering a re-login.
/// </summary>
public sealed class DocmostAuthHandler : DelegatingHandler
{
    private readonly CookieSessionStore _sessionStore;
    private readonly IOptions<DocmostOptions> _options;

    private const int SessionReadyTimeoutSeconds = 10;

    /// <summary>
    /// Initializes the handler with the session store and options.
    /// </summary>
    public DocmostAuthHandler(CookieSessionStore sessionStore, IOptions<DocmostOptions> options)
    {
        _sessionStore = sessionStore ?? throw new ArgumentNullException(nameof(sessionStore));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var opts = _options.Value;

        // Step 1: Ensure session is ready before sending.
        try
        {
            var cookie = await _sessionStore.EnsureFreshSessionAsync(
                opts.Email!,
                opts.Password!,
                cancellationToken);

            request.Headers.Remove("Cookie");
            request.Headers.Add("Cookie", $"{cookie.Name}={cookie.Value}");
        }
        catch (DocmostAuthException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new DocmostAuthException(
                $"Failed to establish session with Docmost: {ex.Message}", ex);
        }

        // Step 2: Send the request.
        var response = await base.SendAsync(request, cancellationToken);

        // Step 3: If 401, retry once after re-login.
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            response.Dispose();

            _sessionStore.MarkUnauthenticated();

            try
            {
                var newCookie = await _sessionStore.EnsureFreshSessionAsync(
                    opts.Email!,
                    opts.Password!,
                    cancellationToken);

                // Clone the request (can't reuse after dispose).
                var retryRequest = await CloneHttpRequestMessageAsync(request, cancellationToken);

                retryRequest.Headers.Remove("Cookie");
                retryRequest.Headers.Add("Cookie", $"{newCookie.Name}={newCookie.Value}");

                response = await base.SendAsync(retryRequest, cancellationToken);

                // If still 401 after retry, credentials are invalid.
                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    throw new DocmostAuthException(
                        "Received 401 from Docmost even after re-authentication. " +
                        "Please verify DOCMOST_EMAIL and DOCMOST_PASSWORD are correct " +
                        "and the account has access to the requested resource.");
                }
            }
            catch (DocmostAuthException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new DocmostAuthException(
                    $"Re-authentication failed: {ex.Message}", ex);
            }
        }

        return response;
    }

    private static async Task<HttpRequestMessage> CloneHttpRequestMessageAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri);

        if (request.Content is not null)
        {
            var contentBytes = await request.Content.ReadAsByteArrayAsync(cancellationToken);
            clone.Content = new ByteArrayContent(contentBytes);

            if (request.Content.Headers.ContentType is not null)
            {
                clone.Content.Headers.ContentType = request.Content.Headers.ContentType;
            }
        }

        foreach (var (key, value) in request.Headers)
        {
            clone.Headers.TryAddWithoutValidation(key, value);
        }

        return clone;
    }
}
