using System.ComponentModel;
using System.Text.Json;
using DocMostMcp.Server.Client;
using DocMostMcp.Server.Json;

namespace DocMostMcp.Server.Tools;

using ModelContextProtocol.Server;

/// <summary>
/// MCP tools that expose Docmost wiki functionality to AI agents.
/// </summary>
[McpServerToolType]
public sealed class DocmostTools
{
    private readonly DocmostClient _client;

    /// <summary>
    /// Initializes the tools with the Docmost API client.
    /// </summary>
    public DocmostTools(DocmostClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    /// <summary>
    /// Lists spaces the authenticated user has access to.
    /// </summary>
    [McpServerTool]
    [Description("Lists spaces the authenticated user has access to. " +
        "Supports optional text filtering via 'query' and cursor-based pagination via 'cursor' and 'limit'.")]
    public async Task<JsonElement> ListSpaces(
        [Description("Optional text filter to search spaces by name or slug.")]
        string? query = null,
        [Description("Maximum number of items to return (1-100, default 20).")]
        int? limit = null,
        [Description("Opaque cursor from a previous response's 'nextCursor' to fetch the next page.")]
        string? cursor = null,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(() =>
            _client.ListSpacesAsync(query, limit, cursor, cancellationToken));
    }

    /// <summary>
    /// Gets detailed information about a specific space.
    /// </summary>
    [McpServerTool]
    [Description("Gets detailed information about a specific space, including member count and " +
        "the current user's membership role and permissions.")]
    public async Task<JsonElement> GetSpaceInfo(
        [Description("Space identifier (UUID).")]
        string spaceId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(spaceId))
        {
            return ErrorResult(400, "spaceId is required and must not be empty.");
        }

        return await ExecuteAsync(() =>
            _client.GetSpaceInfoAsync(spaceId, cancellationToken));
    }

    /// <summary>
    /// Lists pages in the sidebar for a space or under a specific parent page.
    /// </summary>
    [McpServerTool]
    [Description("Lists pages in the sidebar for a space or under a specific parent page. " +
        "At least one of 'spaceId' or 'pageId' must be provided.")]
    public async Task<JsonElement> ListSidebarPages(
        [Description("Space ID to list root pages from.")]
        string? spaceId = null,
        [Description("Parent page ID to list child pages under.")]
        string? pageId = null,
        [Description("Maximum number of items to return (1-100, default 20).")]
        int? limit = null,
        [Description("Opaque cursor from a previous response's 'nextCursor' to fetch the next page.")]
        string? cursor = null,
        [Description("Optional text filter.")]
        string? query = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(spaceId) && string.IsNullOrWhiteSpace(pageId))
        {
            return ErrorResult(400, "At least one of 'spaceId' or 'pageId' must be provided.");
        }

        return await ExecuteAsync(() =>
            _client.ListSidebarPagesAsync(spaceId, pageId, limit, cursor, query, cancellationToken));
    }

    /// <summary>
    /// Gets the full details of a page, optionally including its content.
    /// </summary>
    [McpServerTool]
    [Description("Gets the full details of a page, optionally including its content in markdown format.")]
    public async Task<JsonElement> GetPage(
        [Description("The ID of the page to retrieve.")]
        string pageId,
        [Description("Whether to include the page content (default: true).")]
        bool? includeContent = null,
        [Description("Output format for content: 'markdown' (default), 'json', or 'html'.")]
        string? format = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(pageId))
        {
            return ErrorResult(400, "pageId is required and must not be empty.");
        }

        return await ExecuteAsync(() =>
            _client.GetPageAsync(pageId, includeContent ?? true, format ?? "markdown", cancellationToken));
    }

    /// <summary>
    /// Creates a new page in a space.
    /// </summary>
    [McpServerTool]
    [Description("Creates a new page in a space. Content is in markdown format " +
        "(converted to ProseMirror by Docmost).")]
    public async Task<JsonElement> CreatePage(
        [Description("The space ID where the page will be created.")]
        string spaceId,
        [Description("Page title.")]
        string? title = null,
        [Description("Optional emoji icon for the page.")]
        string? icon = null,
        [Description("Optional parent page ID for creating a sub-page.")]
        string? parentPageId = null,
        [Description("Page content in markdown format.")]
        string? content = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(spaceId))
        {
            return ErrorResult(400, "spaceId is required and must not be empty.");
        }

        return await ExecuteAsync(() =>
            _client.CreatePageAsync(spaceId, title, icon, parentPageId, content, "markdown", cancellationToken));
    }

    /// <summary>
    /// Updates an existing page.
    /// </summary>
    [McpServerTool]
    [Description("Updates an existing page. Supports replacing or appending content. " +
        "Content is in markdown format.")]
    public async Task<JsonElement> UpdatePage(
        [Description("The ID of the page to update.")]
        string pageId,
        [Description("New page title.")]
        string? title = null,
        [Description("New emoji icon.")]
        string? icon = null,
        [Description("New content in markdown format.")]
        string? content = null,
        [Description("Update operation: 'replace' (default), 'append', or 'prepend'.")]
        string? operation = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(pageId))
        {
            return ErrorResult(400, "pageId is required and must not be empty.");
        }

        return await ExecuteAsync(() =>
            _client.UpdatePageAsync(pageId, title, icon, content, operation ?? "replace", "markdown", cancellationToken));
    }

    /// <summary>
    /// Deletes a page.
    /// </summary>
    [McpServerTool]
    [Description("Deletes a page. By default it soft-deletes (moves to trash). " +
        "Set permanentlyDelete=true to hard-delete.")]
    public async Task<JsonElement> DeletePage(
        [Description("The ID of the page to delete.")]
        string pageId,
        [Description("If true, permanently deletes the page. If false (default), moves to trash.")]
        bool? permanentlyDelete = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(pageId))
        {
            return ErrorResult(400, "pageId is required and must not be empty.");
        }

        return await ExecuteAsync(() =>
            _client.DeletePageAsync(pageId, permanentlyDelete ?? false, cancellationToken));
    }

    /// <summary>
    /// Full-text search across pages.
    /// </summary>
    [McpServerTool]
    [Description("Full-text search across pages. Returns ranked results with highlights. " +
        "'query' is required; other parameters are optional filters.")]
    public async Task<JsonElement> SearchPages(
        [Description("The search query (required).")]
        string query,
        [Description("Optional space ID to restrict search to a specific space.")]
        string? spaceId = null,
        [Description("Optional creator user ID to filter by author.")]
        string? creatorId = null,
        [Description("Maximum number of results.")]
        int? limit = null,
        [Description("Number of results to skip for pagination.")]
        int? offset = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return ErrorResult(400, "query is required and must not be empty.");
        }

        return await ExecuteAsync(() =>
            _client.SearchPagesAsync(query, spaceId, creatorId, limit, offset, cancellationToken));
    }

    // ──────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────

    /// <summary>
    /// Wraps a DocmostClient call, translating success/error into the standard JSON envelope.
    /// Catches <see cref="DocmostAuthException"/> and returns a 401 error JSON.
    /// </summary>
    private async Task<JsonElement> ExecuteAsync<T>(Func<Task<DocmostResult<T>>> action)
    {
        try
        {
            var result = await action();

            if (result.IsSuccess)
            {
                var response = new OkResponse<T?>(
                    Ok: true,
                    StatusCode: result.StatusCode,
                    Data: result.Data
                );
                return JsonSerializer.SerializeToElement(response, typeof(OkResponse<T?>), AppJsonSerializerContext.Default);
            }

            // Recoverable error from Docmost
            var errorResponse = new ErrorResponse(
                Ok: false,
                StatusCode: result.StatusCode,
                Error: result.Error?.Error ?? "Unknown error",
                Details: result.Error?.Message
            );
            return JsonSerializer.SerializeToElement(errorResponse, typeof(ErrorResponse), AppJsonSerializerContext.Default);
        }
        catch (DocmostAuthException ex)
        {
            var authError = new ErrorResponse(
                Ok: false,
                StatusCode: 401,
                Error: ex.Message
            );
            return JsonSerializer.SerializeToElement(authError, typeof(ErrorResponse), AppJsonSerializerContext.Default);
        }
    }

    /// <summary>
    /// Creates a JSON error result without calling the API (for parameter validation failures).
    /// </summary>
    private static JsonElement ErrorResult(int statusCode, string message)
    {
        var error = new ErrorResponse(
            Ok: false,
            StatusCode: statusCode,
            Error: message
        );
        return JsonSerializer.SerializeToElement(error, typeof(ErrorResponse), AppJsonSerializerContext.Default);
    }
}
