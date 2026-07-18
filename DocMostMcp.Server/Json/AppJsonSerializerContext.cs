using System.Text.Json;
using System.Text.Json.Serialization;
using DocMostMcp.Server.Client;
using DocMostMcp.Server.Client.Models;
using DocMostMcp.Server.Tools;

namespace DocMostMcp.Server.Json;

/// <summary>
/// Source-generated JSON serializer context for all types that cross the
/// serialization boundary in the MCP server. This enables Native AOT publishing
/// by eliminating runtime reflection in System.Text.Json.
/// </summary>
[JsonSourceGenerationOptions(
    JsonSerializerDefaults.Web,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = false,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(OkResponse<object>))]
[JsonSerializable(typeof(OkResponse<JsonElement>))]
[JsonSerializable(typeof(OkResponse<Space?>))]
[JsonSerializable(typeof(OkResponse<SpacesListResponse?>))]
[JsonSerializable(typeof(OkResponse<SidebarPagesResponse?>))]
[JsonSerializable(typeof(OkResponse<Page?>))]
[JsonSerializable(typeof(OkResponse<SearchResponse?>))]
[JsonSerializable(typeof(ErrorResponse))]
[JsonSerializable(typeof(Space))]
[JsonSerializable(typeof(SpaceMembership))]
[JsonSerializable(typeof(SpacePermission))]
[JsonSerializable(typeof(Page))]
[JsonSerializable(typeof(PageUser))]
[JsonSerializable(typeof(PageSpaceRef))]
[JsonSerializable(typeof(SidebarPageItem))]
[JsonSerializable(typeof(SidebarPagesResponse))]
[JsonSerializable(typeof(SpacesListResponse))]
[JsonSerializable(typeof(SearchResponse))]
[JsonSerializable(typeof(SearchResult))]
[JsonSerializable(typeof(SearchResultSpace))]
[JsonSerializable(typeof(PaginationMeta))]
[JsonSerializable(typeof(ApiError))]
[JsonSerializable(typeof(ApiResponse<object>))]
internal partial class AppJsonSerializerContext : JsonSerializerContext
{
}
