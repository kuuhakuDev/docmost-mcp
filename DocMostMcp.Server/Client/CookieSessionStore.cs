using System.Net;
using DocMostMcp.Server.Client.Models;

namespace DocMostMcp.Server.Client;

/// <summary>
/// Thread-safe singleton store for the Docmost session cookie.
/// Serializes concurrent login attempts with <c>SemaphoreSlim(1,1)</c>
/// and marks credentials as terminal on failure to prevent aggressive retries.
/// </summary>
public sealed class CookieSessionStore : IDisposable
{
    private readonly SemaphoreSlim _loginLock = new(1, 1);
    private readonly CookieContainer _cookieContainer = new();
    private readonly HttpClient _httpClient;

    private bool _isAuthenticated;
    private DateTime? _lastLoginAt;
    private bool _isTerminallyUnauthenticated;
    private string? _terminalError;

    private const int LoginTimeoutSeconds = 10;

    /// <summary>
    /// Initializes the store with an <see cref="HttpClient"/> used exclusively for login calls.
    /// This client must NOT go through <see cref="DocmostAuthHandler"/> to avoid infinite recursion.
    /// </summary>
    public CookieSessionStore(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    /// <summary>Whether the store currently holds a valid session cookie.</summary>
    public bool IsAuthenticated => _isAuthenticated && !_isTerminallyUnauthenticated;

    /// <summary>Timestamp of the last successful login.</summary>
    public DateTime? LastLoginAt => _lastLoginAt;

    /// <summary>
    /// Ensures a fresh session cookie is available. If the store is already authenticated,
    /// returns the existing cookie immediately. Otherwise, performs a login.
    /// </summary>
    /// <param name="email">Docmost account email.</param>
    /// <param name="password">Docmost account password.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The session <see cref="Cookie"/>.</returns>
    /// <exception cref="DocmostAuthException">
    /// Thrown if login fails or credentials are invalid.
    /// </exception>
    /// <exception cref="TimeoutException">
    /// Thrown if the login lock cannot be acquired within <see cref="LoginTimeoutSeconds"/> seconds.
    /// </exception>
    public async Task<Cookie> EnsureFreshSessionAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        // Fast path: already authenticated.
        if (_isAuthenticated && !_isTerminallyUnauthenticated)
        {
            return GetSessionCookie();
        }

        // Terminal failure — don't retry.
        if (_isTerminallyUnauthenticated)
        {
            throw new DocmostAuthException(
                $"Cannot authenticate with Docmost: {_terminalError}");
        }

        var acquired = await _loginLock.WaitAsync(
            TimeSpan.FromSeconds(LoginTimeoutSeconds),
            cancellationToken);

        if (!acquired)
        {
            throw new TimeoutException(
                $"Could not acquire login semaphore within {LoginTimeoutSeconds} seconds. " +
                "Another login attempt may be hung.");
        }

        try
        {
            // Double-check after acquiring lock (another thread may have logged in).
            if (_isAuthenticated && !_isTerminallyUnauthenticated)
            {
                return GetSessionCookie();
            }

            await LoginAsync(email, password, cancellationToken);
            return GetSessionCookie();
        }
        finally
        {
            _loginLock.Release();
        }
    }

    /// <summary>
    /// Marks the session as unauthenticated (e.g. after receiving a 401).
    /// Does NOT clear the terminal state if already terminal.
    /// </summary>
    public void MarkUnauthenticated()
    {
        _isAuthenticated = false;
    }

    /// <summary>
    /// Gets the current session cookie, or throws if no cookie is available.
    /// </summary>
    private Cookie GetSessionCookie()
    {
        var cookies = _cookieContainer.GetAllCookies();
        // Docmost uses "docmost.sid" or similar; return the first available cookie.
        if (cookies.Count > 0)
        {
            return cookies[0];
        }

        throw new DocmostAuthException("No session cookie available. Please login first.");
    }

    private async Task LoginAsync(string email, string password, CancellationToken cancellationToken)
    {
        var request = new LoginRequest(email, password);
        var response = await _httpClient.PostAsJsonAsync(
            "/api/auth/login",
            request,
            cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            // Extract the Set-Cookie header and store in container.
            var setCookieHeader = response.Headers.SingleOrDefault(
                h => h.Key.Equals("Set-Cookie", StringComparison.OrdinalIgnoreCase));

            if (setCookieHeader.Value is not null)
            {
                foreach (var cookieValue in setCookieHeader.Value)
                {
                    _cookieContainer.SetCookies(
                        response.RequestMessage?.RequestUri ?? _httpClient.BaseAddress!,
                        cookieValue);
                }
            }

            _isAuthenticated = true;
            _isTerminallyUnauthenticated = false;
            _terminalError = null;
            _lastLoginAt = DateTime.UtcNow;
        }
        else
        {
            // Read error body.
            var body = await response.Content.ReadFromJsonAsync<ApiError>(
                cancellationToken: cancellationToken);

            var errorMessage = body?.Error switch
            {
                not null when response.StatusCode == HttpStatusCode.Unauthorized =>
                    $"Invalid credentials. Please verify DOCMOST_EMAIL and DOCMOST_PASSWORD. " +
                    $"Server error: {body.Error}",
                not null => $"Login failed: {body.Error}",
                _ => $"Login failed with HTTP {response.StatusCode}."
            };

            // Terminal state — do not retry on next request.
            _isTerminallyUnauthenticated = true;
            _terminalError = errorMessage;

            throw new DocmostAuthException(errorMessage);
        }
    }

    public void Dispose()
    {
        _loginLock.Dispose();
    }
}
