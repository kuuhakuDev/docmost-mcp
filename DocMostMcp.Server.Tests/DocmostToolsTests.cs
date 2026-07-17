using System.Net;
using System.Text.Json;
using DocMostMcp.Server.Client;
using DocMostMcp.Server.Tools;
using FluentAssertions;

namespace DocMostMcp.Server.Tests;

public class DocmostToolsTests
{
    private static DocmostClient CreateClientWithResponse(string json, HttpStatusCode status = HttpStatusCode.OK)
    {
        var handler = new MockHttpMessageHandler(_ =>
            new HttpResponseMessage(status)
            {
                Content = new StringContent(json),
            });

        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:3000"),
        };

        return new DocmostClient(httpClient);
    }

    [Fact]
    public async Task ListSpaces_Success_ReturnsOkWithData()
    {
        // Arrange
        var client = CreateClientWithResponse("""
            {
                "success": true,
                "status": 200,
                "data": {
                    "items": [
                        { "id": "space-1", "name": "Engineering", "slug": "engineering", "defaultRole": "member", "visibility": "public", "workspaceId": "ws-1", "createdAt": "2024-01-01T00:00:00Z", "updatedAt": "2024-01-01T00:00:00Z", "memberCount": 5 }
                    ],
                    "meta": { "limit": 20, "hasNextPage": false, "hasPrevPage": false, "nextCursor": null, "prevCursor": null }
                }
            }
            """);
        var tools = new DocmostTools(client);

        // Act
        var result = await tools.ListSpaces();

        // Assert
        var json = result.GetRawText();
        json.Should().Contain("\"ok\":true");
        json.Should().Contain("\"statusCode\":200");
        json.Should().Contain("Engineering");
    }

    [Fact]
    public async Task ListSpaces_ApiError_ReturnsOkFalse()
    {
        // Arrange
        var client = CreateClientWithResponse("""
            {
                "success": false,
                "status": 401,
                "statusCode": 401,
                "error": "Unauthorized",
                "message": "Invalid credentials"
            }
            """, HttpStatusCode.Unauthorized);
        var tools = new DocmostTools(client);

        // Act
        var result = await tools.ListSpaces();

        // Assert
        var json = result.GetRawText();
        json.Should().Contain("\"ok\":false");
        json.Should().Contain("\"statusCode\":401");
    }

    [Fact]
    public async Task GetSpaceInfo_Success_ReturnsOk()
    {
        // Arrange
        var client = CreateClientWithResponse("""
            {
                "success": true,
                "status": 200,
                "data": {
                    "id": "space-1", "name": "Engineering", "slug": "engineering",
                    "defaultRole": "member", "visibility": "public",
                    "workspaceId": "ws-1", "createdAt": "2024-01-01T00:00:00Z",
                    "updatedAt": "2024-01-01T00:00:00Z", "memberCount": 10,
                    "membership": { "userId": "user-1", "role": "admin", "permissions": [] }
                }
            }
            """);
        var tools = new DocmostTools(client);

        // Act
        var result = await tools.GetSpaceInfo("space-1");

        // Assert
        var json = result.GetRawText();
        json.Should().Contain("\"ok\":true");
        json.Should().Contain("Engineering");
        json.Should().Contain("admin");
    }

    [Fact]
    public async Task GetSpaceInfo_EmptyId_ReturnsValidationError()
    {
        var client = CreateClientWithResponse("{}");
        var tools = new DocmostTools(client);

        var result = await tools.GetSpaceInfo("");

        var json = result.GetRawText();
        json.Should().Contain("\"ok\":false");
        json.Should().Contain("\"statusCode\":400");
        json.Should().Contain("spaceId is required");
    }

    [Fact]
    public async Task ListSidebarPages_MissingBothIds_ReturnsValidationError()
    {
        var client = CreateClientWithResponse("{}");
        var tools = new DocmostTools(client);

        var result = await tools.ListSidebarPages();

        var json = result.GetRawText();
        json.Should().Contain("\"ok\":false");
        json.Should().Contain("spaceId");
    }

    [Fact]
    public async Task CreatePage_WithoutSpaceId_ReturnsValidationError()
    {
        var client = CreateClientWithResponse("{}");
        var tools = new DocmostTools(client);

        var result = await tools.CreatePage("");

        var json = result.GetRawText();
        json.Should().Contain("\"ok\":false");
        json.Should().Contain("spaceId is required");
    }

    [Fact]
    public async Task CreatePage_WithMarkdown_ReturnsOk()
    {
        // Arrange
        var client = CreateClientWithResponse("""
            {
                "success": true,
                "status": 200,
                "data": {
                    "id": "page-new",
                    "slugId": "hello-world",
                    "title": "Hello World",
                    "spaceId": "space-1",
                    "workspaceId": "ws-1",
                    "createdAt": "2024-01-01T00:00:00Z",
                    "updatedAt": "2024-01-01T00:00:00Z",
                    "isLocked": false
                }
            }
            """);
        var tools = new DocmostTools(client);

        // Act
        var result = await tools.CreatePage("space-1", title: "Hello World", content: "# Hello");

        // Assert
        var json = result.GetRawText();
        json.Should().Contain("\"ok\":true");
        json.Should().Contain("page-new");
        json.Should().Contain("Hello World");
    }

    [Fact]
    public async Task DeletePage_WithoutPageId_ReturnsValidationError()
    {
        var client = CreateClientWithResponse("{}");
        var tools = new DocmostTools(client);

        var result = await tools.DeletePage("");

        var json = result.GetRawText();
        json.Should().Contain("\"ok\":false");
        json.Should().Contain("pageId is required");
    }

    [Fact]
    public async Task SearchPages_WithoutQuery_ReturnsValidationError()
    {
        var client = CreateClientWithResponse("{}");
        var tools = new DocmostTools(client);

        var result = await tools.SearchPages("");

        var json = result.GetRawText();
        json.Should().Contain("\"ok\":false");
        json.Should().Contain("query is required");
    }

    [Fact]
    public async Task SearchPages_Success_ReturnsResults()
    {
        // Arrange
        var client = CreateClientWithResponse("""
            {
                "success": true,
                "status": 200,
                "data": {
                    "items": [
                        {
                            "id": "page-1", "title": "Getting Started",
                            "highlight": "start with <mark>setup</mark>",
                            "rank": 0.95,
                            "createdAt": "2024-01-01T00:00:00Z",
                            "updatedAt": "2024-01-01T00:00:00Z",
                            "space": { "id": "space-1", "name": "Docs", "slug": "docs" }
                        }
                    ]
                }
            }
            """);
        var tools = new DocmostTools(client);

        // Act
        var result = await tools.SearchPages("setup");

        // Assert
        var json = result.GetRawText();
        json.Should().Contain("\"ok\":true");
        json.Should().Contain("Getting Started");
        json.Should().Contain("0.95");
    }

    [Fact]
    public async Task GetPage_Success_ReturnsPage()
    {
        // Arrange
        var client = CreateClientWithResponse("""
            {
                "success": true,
                "status": 200,
                "data": {
                    "id": "page-1", "slugId": "my-page",
                    "title": "My Page", "spaceId": "space-1",
                    "workspaceId": "ws-1", "isLocked": false,
                    "content": "# Hello from Docmost",
                    "createdAt": "2024-01-01T00:00:00Z",
                    "updatedAt": "2024-01-01T00:00:00Z"
                }
            }
            """);
        var tools = new DocmostTools(client);

        // Act
        var result = await tools.GetPage("page-1");

        // Assert
        var json = result.GetRawText();
        json.Should().Contain("\"ok\":true");
        json.Should().Contain("My Page");
        json.Should().Contain("Hello from Docmost");
    }

    [Fact]
    public async Task UpdatePage_Success_ReturnsOk()
    {
        // Arrange
        var client = CreateClientWithResponse("""
            {
                "success": true,
                "status": 200,
                "data": {
                    "id": "page-1", "slugId": "my-page",
                    "title": "Updated Title", "spaceId": "space-1",
                    "workspaceId": "ws-1", "isLocked": false,
                    "createdAt": "2024-01-01T00:00:00Z",
                    "updatedAt": "2024-02-01T00:00:00Z"
                }
            }
            """);
        var tools = new DocmostTools(client);

        // Act
        var result = await tools.UpdatePage("page-1", title: "Updated Title");

        // Assert
        var json = result.GetRawText();
        json.Should().Contain("\"ok\":true");
        json.Should().Contain("Updated Title");
    }

    [Fact]
    public async Task DeletePage_Success_ReturnsOk()
    {
        // Arrange
        var client = CreateClientWithResponse("""
            {
                "success": true,
                "status": 200,
                "data": null
            }
            """);
        var tools = new DocmostTools(client);

        // Act
        var result = await tools.DeletePage("page-1");

        // Assert
        var json = result.GetRawText();
        json.Should().Contain("\"ok\":true");
        json.Should().Contain("\"statusCode\":200");
    }
}
