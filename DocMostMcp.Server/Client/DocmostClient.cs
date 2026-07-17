using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DocMostMcp.Server.Client.Models;

namespace DocMostMcp.Server.Client;

/// <summary>
/// Typed façade over the Docmost HTTP API. Each method wraps a POST call with
/// structured JSON serialization of request/response.
/// </summary>
public sealed class DocmostClient
{
    private readonly HttpClient _httpClient;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// Initializes the client with an <see cref="HttpClient"/> that goes through
    /// <see cref="DocmostAuthHandler"/> for automatic cookie injection and retry.
    /// </summary>
    public DocmostClient(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    /// <summary>
    /// Lists spaces the authenticated user has access to.
    /// POST /api/spaces
    /// </summary>
    public async Task<DocmostResult<SpacesListResponse>> ListSpacesAsync(
        string? query = null,
        int? limit = null,
        string? cursor = null,
        CancellationToken cancellationToken = default)
    {
        var body = new Dictionary<string, object?>();
        if (query is not null) body["query"] = query;
        if (limit is not null) body["limit"] = limit;
        if (cursor is not null) body["cursor"] = cursor;

        return await PostAsync<SpacesListResponse>("/api/spaces", body, cancellationToken);
    }

    /// <summary>
    /// Gets detailed information about a specific space.
    /// POST /api/spaces/info
    /// </summary>
    public async Task<DocmostResult<Space>> GetSpaceInfoAsync(
        string spaceId,
        CancellationToken cancellationToken = default)
    {
        var body = new Dictionary<string, object?> { ["spaceId"] = spaceId };
        return await PostAsync<Space>("/api/spaces/info", body, cancellationToken);
    }

    /// <summary>
    /// Lists pages in the sidebar for a space or under a specific parent page.
    /// POST /api/pages/sidebar-pages
    /// </summary>
    public async Task<DocmostResult<SidebarPagesResponse>> ListSidebarPagesAsync(
        string? spaceId = null,
        string? pageId = null,
        int? limit = null,
        string? cursor = null,
        string? query = null,
        CancellationToken cancellationToken = default)
    {
        var body = new Dictionary<string, object?>();
        if (spaceId is not null) body["spaceId"] = spaceId;
        if (pageId is not null) body["pageId"] = pageId;
        if (limit is not null) body["limit"] = limit;
        if (cursor is not null) body["cursor"] = cursor;
        if (query is not null) body["query"] = query;

        return await PostAsync<SidebarPagesResponse>("/api/pages/sidebar-pages", body, cancellationToken);
    }

    /// <summary>
    /// Gets the full details of a page, optionally including content.
    /// POST /api/pages/info
    /// </summary>
    public async Task<DocmostResult<Page>> GetPageAsync(
        string pageId,
        bool? includeContent = null,
        string? format = null,
        CancellationToken cancellationToken = default)
    {
        var body = new Dictionary<string, object?> { ["pageId"] = pageId };
        if (includeContent is not null) body["includeContent"] = includeContent;
        if (format is not null) body["format"] = format;

        return await PostAsync<Page>("/api/pages/info", body, cancellationToken);
    }

    /// <summary>
    /// Creates a new page in a space.
    /// POST /api/pages/create
    /// </summary>
    public async Task<DocmostResult<Page>> CreatePageAsync(
        string spaceId,
        string? title = null,
        string? icon = null,
        string? parentPageId = null,
        string? content = null,
        string? format = null,
        CancellationToken cancellationToken = default)
    {
        var body = new Dictionary<string, object?> { ["spaceId"] = spaceId };
        if (title is not null) body["title"] = title;
        if (icon is not null) body["icon"] = icon;
        if (parentPageId is not null) body["parentPageId"] = parentPageId;
        if (content is not null) body["content"] = content;
        if (format is not null) body["format"] = format;

        return await PostAsync<Page>("/api/pages/create", body, cancellationToken);
    }

    /// <summary>
    /// Updates an existing page.
    /// POST /api/pages/update
    /// </summary>
    public async Task<DocmostResult<Page>> UpdatePageAsync(
        string pageId,
        string? title = null,
        string? icon = null,
        string? content = null,
        string? operation = null,
        string? format = null,
        CancellationToken cancellationToken = default)
    {
        var body = new Dictionary<string, object?> { ["pageId"] = pageId };
        if (title is not null) body["title"] = title;
        if (icon is not null) body["icon"] = icon;
        if (content is not null) body["content"] = content;
        if (operation is not null) body["operation"] = operation;
        if (format is not null) body["format"] = format;

        return await PostAsync<Page>("/api/pages/update", body, cancellationToken);
    }

    /// <summary>
    /// Deletes a page (soft or permanent).
    /// POST /api/pages/delete
    /// </summary>
    public async Task<DocmostResult<object>> DeletePageAsync(
        string pageId,
        bool? permanentlyDelete = null,
        CancellationToken cancellationToken = default)
    {
        var body = new Dictionary<string, object?> { ["pageId"] = pageId };
        if (permanentlyDelete is not null) body["permanentlyDelete"] = permanentlyDelete;

        return await PostAsync<object>("/api/pages/delete", body, cancellationToken);
    }

    /// <summary>
    /// Full-text search across pages.
    /// POST /api/search
    /// </summary>
    public async Task<DocmostResult<SearchResponse>> SearchPagesAsync(
        string query,
        string? spaceId = null,
        string? creatorId = null,
        int? limit = null,
        int? offset = null,
        CancellationToken cancellationToken = default)
    {
        var body = new Dictionary<string, object?> { ["query"] = query };
        if (spaceId is not null) body["spaceId"] = spaceId;
        if (creatorId is not null) body["creatorId"] = creatorId;
        if (limit is not null) body["limit"] = limit;
        if (offset is not null) body["offset"] = offset;

        return await PostAsync<SearchResponse>("/api/search", body, cancellationToken);
    }

    // ──────────────────────────────────────────────────
    // Internal helpers
    // ──────────────────────────────────────────────────

    /// <summary>
    /// Internal POST helper that sends a request and parses the response.
    /// Returns either parsed data or an <see cref="ApiError"/> for Docmost errors.
    /// Throws on network failures or unexpected responses.
    /// </summary>
    private async Task<DocmostResult<T>> PostAsync<T>(
        string endpoint,
        object? body,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync(endpoint, body, JsonOptions, cancellationToken);

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (string.IsNullOrWhiteSpace(responseBody))
            {
                return response.IsSuccessStatusCode
                    ? DocmostResult<T>.Success(default, (int)response.StatusCode)
                    : DocmostResult<T>.Failure(new ApiError
                    {
                        StatusCode = (int)response.StatusCode,
                        Error = response.ReasonPhrase ?? "Unknown error",
                    });
            }

            using var document = JsonDocument.Parse(responseBody);
            var root = document.RootElement;

            // Try parsing as ApiResponse<T>
            if (root.TryGetProperty("success", out var successProp))
            {
                var success = successProp.ValueKind == JsonValueKind.True;

                if (success)
                {
                    var data = default(T);
                    if (root.TryGetProperty("data", out var dataProp) && dataProp.ValueKind != JsonValueKind.Null)
                    {
                        data = JsonSerializer.Deserialize<T>(dataProp.GetRawText(), JsonOptions);
                    }

                    return DocmostResult<T>.Success(data, (int)response.StatusCode);
                }

                // Error response from Docmost
                var apiError = ApiError.FromJsonElement(root) ?? new ApiError
                {
                    StatusCode = (int)response.StatusCode,
                    Error = "Unknown Docmost error",
                };

                return DocmostResult<T>.Failure(apiError);
            }

            // Not a wrapped response — treat based on status code
            if (response.IsSuccessStatusCode)
            {
                var data = JsonSerializer.Deserialize<T>(responseBody, JsonOptions);
                return DocmostResult<T>.Success(data, (int)response.StatusCode);
            }

            return DocmostResult<T>.Failure(new ApiError
            {
                StatusCode = (int)response.StatusCode,
                Error = response.ReasonPhrase ?? "Unknown error",
            });
        }
        catch (DocmostAuthException)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            throw new DocmostAuthException(
                $"Network error while connecting to Docmost: {ex.Message}", ex);
        }
        catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new DocmostAuthException(
                $"Unexpected error communicating with Docmost: {ex.Message}", ex);
        }
    }
}

// ─────────────────────────────────────────────────────
// Result types
// ─────────────────────────────────────────────────────

/// <summary>
/// Represents the result of a Docmost API call, either success or failure.
/// </summary>
public sealed class DocmostResult<T>
{
    private DocmostResult() { }

    /// <summary>Whether the API call succeeded.</summary>
    public bool IsSuccess { get; private init; }

    /// <summary>Parsed data on success.</summary>
    public T? Data { get; private init; }

    /// <summary>Parsed error on failure.</summary>
    public ApiError? Error { get; private init; }

    /// <summary>HTTP status code of the response.</summary>
    public int StatusCode { get; private init; }

    /// <summary>Creates a success result.</summary>
    public static DocmostResult<T> Success(T? data, int statusCode) => new()
    {
        IsSuccess = true,
        Data = data,
        StatusCode = statusCode,
    };

    /// <summary>Creates a failure result.</summary>
    public static DocmostResult<T> Failure(ApiError error) => new()
    {
        IsSuccess = false,
        Error = error,
        StatusCode = error.StatusCode,
    };
}

// ─────────────────────────────────────────────────────
// Response models for list_spaces
// ─────────────────────────────────────────────────────

/// <summary>
/// Response payload for POST /api/spaces.
/// </summary>
public sealed class SpacesListResponse
{
    /// <summary>List of spaces.</summary>
    public Space[] Items { get; set; } = [];

    /// <summary>Pagination metadata.</summary>
    public PaginationMeta? Meta { get; set; }
}

/// <summary>
/// Response payload for POST /api/pages/sidebar-pages.
/// </summary>
public sealed class SidebarPagesResponse
{
    /// <summary>List of sidebar page items.</summary>
    public SidebarPageItem[] Items { get; set; } = [];

    /// <summary>Pagination metadata.</summary>
    public PaginationMeta? Meta { get; set; }
}

/// <summary>
/// A sidebar page item (lightweight, no content).
/// </summary>
public sealed class SidebarPageItem
{
    [System.Text.Json.Serialization.JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("slugId")]
    public string SlugId { get; set; } = string.Empty;

    [System.Text.Json.Serialization.JsonPropertyName("title")]
    public string? Title { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("icon")]
    public string? Icon { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("position")]
    public string? Position { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("parentPageId")]
    public string? ParentPageId { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("hasChildren")]
    public bool HasChildren { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("spaceId")]
    public string SpaceId { get; set; } = string.Empty;
}

/// <summary>
/// Response payload for POST /api/search.
/// </summary>
public sealed class SearchResponse
{
    /// <summary>List of search results.</summary>
    public SearchResult[] Items { get; set; } = [];
}
