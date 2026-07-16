## 1. Configuración del proyecto

- [ ] 1.1 Modificar `DocMostMcp.Server/DocMostMcp.Server.csproj`: cambiar `Sdk` a `Microsoft.NET.Sdk.Web`, añadir `PackageReference` a `ModelContextProtocol.AspNetCore` 1.2.0, actualizar `PackageId`, `Description`, `PackageTags`, `PackageVersion` a valores reales.
- [ ] 1.2 Eliminar `DocMostMcp.Server/Tools/RandomNumberTools.cs` (placeholder del template).
- [ ] 1.3 Crear `DocMostMcp.Server.Tests/DocMostMcp.Server.Tests.csproj` con `Sdk=Microsoft.NET.Sdk`, `TargetFramework=net10.0`, `<IsPackable>false</IsPackable>` y referencias a `Microsoft.NET.Test.Sdk`, `xunit`, `xunit.runner.visualstudio`, `NSubstitute`, `AwesomeAssertions`, y `ProjectReference` a `DocMostMcp.Server.csproj`.
- [ ] 1.4 Añadir el proyecto de tests a `DocmostMcp.slnx`.
- [ ] 1.5 Actualizar `DocMostMcp.Server/.mcp/server.json` con `name`, `description`, `version`, `repository.url` reales, e incluir en `environmentVariables` las cinco variables esperadas (`DOCMOST_MCP_TRANSPORT`, `DOCMOST_MCP_PORT`, `DOCMOST_URL`, `DOCMOST_EMAIL`, `DOCMOST_PASSWORD`).
- [ ] 1.6 Verificar que `dotnet build DocmostMcp.slnx -c Debug` compila el esqueleto sin errores.

## 2. Configuración tipada y validación

- [ ] 2.1 Crear `DocMostMcp.Server/Configuration/DocmostOptions.cs` con propiedades `Url` (Uri), `Email` (string), `Password` (string), `Port` (int, default 3001), `Transport` (enum `TransportMode { Stdio, Http }`, default por TTY).
- [ ] 2.2 Crear `DocMostMcp.Server/Configuration/DocmostOptionsValidator.cs` que implemente `IValidateOptions<DocmostOptions>` y falle con mensaje claro si falta `Url`, `Email` o `Password`, o si `Port` está fuera de `[1, 65535]`, o si `Transport` tiene un valor no parseable.
- [ ] 2.3 Implementar la lectura de variables de entorno con `ConfigurationBuilder` + `EnvironmentVariablesConfigurationSource` y enlace a `DocmostOptions` con `services.AddOptions<DocmostOptions>().Bind(...).Validate(...).ValidateOnStart()`.
- [ ] 2.4 Implementar la lógica de "transporte por defecto con TTY" usando `Console.IsInputRedirected`.

## 3. Modelos de datos (DTOs)

- [ ] 3.1 Crear `DocMostMcp.Server/Client/Models/ApiResponse.cs` con `ApiResponse<T>` (`bool Success`, `int Status`, `T? Data`).
- [ ] 3.2 Crear `DocMostMcp.Server/Client/Models/ApiError.cs` con `int StatusCode`, `string Message`, `string? Error`.
- [ ] 3.3 Crear `DocMostMcp.Server/Client/Models/LoginRequest.cs` con `Email` y `Password`.
- [ ] 3.4 Crear `DocMostMcp.Server/Client/Models/Space.cs` con los campos de `spaces/spaces.json` y `spaces/spaces-info.json` (id, name, slug, description, logo, defaultRole, visibility, settings, creatorId, workspaceId, createdAt, updatedAt, memberCount, membership).
- [ ] 3.5 Crear `DocMostMcp.Server/Client/Models/Page.cs` con los campos comunes de pages (id, slugId, title, icon, content, position, parentPageId, spaceId, creatorId, lastUpdatedById, isLocked, contributorIds, workspaceId, createdAt, updatedAt, deletedAt).
- [ ] 3.6 Crear `DocMostMcp.Server/Client/Models/SearchResult.cs` con los campos de `search/search.json` (id, title, icon, parentPageId, creatorId, rank, highlight, createdAt, updatedAt, space).
- [ ] 3.7 Crear `DocMostMcp.Server/Client/Models/PaginationMeta.cs` con `Limit`, `HasNextPage`, `HasPrevPage`, `NextCursor`, `PrevCursor`.

## 4. Cliente HTTP de Docmost (autenticación transparente)

- [ ] 4.1 Crear `DocMostMcp.Server/Client/CookieSessionStore.cs`: clase singleton (DI) que envuelve un `CookieContainer`, una `SemaphoreSlim(1,1)`, y mantiene `IsAuthenticated` y `LastLoginAt`. Método público `Task<Cookie> EnsureFreshSessionAsync(string email, string password, CancellationToken)` que serializa llamadas a `POST /api/auth/login`.
- [ ] 4.2 En `CookieSessionStore`, manejar el caso de credenciales inválidas marcándolo como estado terminal hasta el próximo arranque (no reloguear agresivamente).
- [ ] 4.3 Crear `DocMostMcp.Server/Client/DocmostAuthHandler.cs`: `DelegatingHandler` que en cada request verifica con `CookieSessionStore` que la sesión esté lista, y en `SendAsync` captura respuestas 401, llama a `EnsureFreshSessionAsync`, y reenvía la request **una sola vez**. Si la segunda respuesta también es 401, lanza `DocmostAuthException`.
- [ ] 4.4 Crear `DocMostMcp.Server/Client/DocmostAuthException.cs` con mensaje por defecto que mencione `DOCMOST_EMAIL` y `DOCMOST_PASSWORD`.
- [ ] 4.5 Crear `DocMostMcp.Server/Client/DocmostClient.cs`: clase façade con `HttpClient` inyectado y métodos tipados (`ListSpacesAsync`, `GetSpaceInfoAsync`, `ListSidebarPagesAsync`, `GetPageAsync`, `CreatePageAsync`, `UpdatePageAsync`, `DeletePageAsync`, `SearchPagesAsync`). Cada método construye el body, llama a `PostAsJsonAsync` y parsea la respuesta envuelta.
- [ ] 4.6 En `DocmostClient`, distinguir entre éxito (`Success=true`), error recuperable de Docmost (devolver `ApiError` parseado), y excepciones (lanzar). El método `PostAsync` interno devuelve `JsonElement? data` o un `ApiError` para que el tool decida cómo formatear.
- [ ] 4.7 Registrar en DI: `services.AddSingleton<CookieSessionStore>()`, `services.AddHttpClient<DocmostClient>().AddHttpMessageHandler<DocmostAuthHandler>().ConfigureHttpClient(c => c.BaseAddress = options.Url).ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { UseCookies = false })` (las cookies las gestiona `CookieSessionStore` directamente, no `HttpClient`).

## 5. Tools MCP

- [ ] 5.1 Crear `DocMostMcp.Server/Tools/DocmostTools.cs` con `[McpServerToolType]` en la clase. Inyectar `DocmostClient` por constructor.
- [ ] 5.2 Implementar tool `list_spaces(query?, limit?, cursor?)` con `[McpServerTool]` y `[Description]`. Llama a `DocmostClient.ListSpacesAsync` y devuelve JSON `{ ok, statusCode, data: { items, meta } }`.
- [ ] 5.3 Implementar tool `get_space_info(spaceId)` con validación de parámetro obligatorio.
- [ ] 5.4 Implementar tool `list_sidebar_pages(spaceId?, pageId?, limit?, cursor?, query?)` con validación de al menos uno entre `spaceId` y `pageId`.
- [ ] 5.5 Implementar tool `get_page(pageId, includeContent?, format?)` con default `format="markdown"` y default `includeContent=true`.
- [ ] 5.6 Implementar tool `create_page(spaceId, title?, icon?, parentPageId?, content?)` que internamente envía `format="markdown"`. Validar que `spaceId` esté presente.
- [ ] 5.7 Implementar tool `update_page(pageId, title?, icon?, content?, operation?)` con default `operation="replace"` y `format="markdown"` interno. Validar `pageId`.
- [ ] 5.8 Implementar tool `delete_page(pageId, permanentlyDelete?)` con default `permanentlyDelete=false`. Validar `pageId`.
- [ ] 5.9 Implementar tool `search_pages(query, spaceId?, creatorId?, limit?, offset?)` con validación de `query` obligatorio.
- [ ] 5.10 Cada tool debe capturar `DocmostAuthException` y devolver un JSON `{ ok: false, statusCode: 401, error: "<mensaje de la excepción>" }` sin lanzar.

## 6. Bootstrap del programa (Program.cs)

- [ ] 6.1 Reescribir `DocMostMcp.Server/Program.cs`: en el top-level, leer `DocmostOptions`, decidir el modo (`stdio` o `http`).
- [ ] 6.2 Rama `stdio`: usar `Host.CreateApplicationBuilder(args)`, registrar `AddMcpServer().WithStdioServerTransport().WithToolsFromAssembly()`, registrar `DocmostClient` + `CookieSessionStore` + `DocmostAuthHandler`, y `await builder.Build().RunAsync()`.
- [ ] 6.3 Rama `http`: usar `WebApplication.CreateBuilder(args)`, registrar `AddMcpServer().WithHttpTransport(o => o.Stateless = true).WithToolsFromAssembly()`, registrar `DocmostClient` + `CookieSessionStore` + `DocmostAuthHandler`, y `app.MapMcp()` + `app.RunAsync($"http://0.0.0.0:{port}")`.
- [ ] 6.4 Configurar el logging para que todos los mensajes vayan a stderr (no contaminar stdout que es el canal del protocolo MCP en stdio).
- [ ] 6.5 Verificar que `dotnet run --project DocMostMcp.Server` con las variables de entorno correctas arranca en ambos modos.

## 7. Tests unitarios

- [ ] 7.1 Crear `DocMostMcp.Server.Tests/CookieSessionStoreTests.cs`: tests de login exitoso, login con credenciales inválidas (estado terminal), serialización con `SemaphoreSlim` (N tareas paralelas → un solo POST), timeout de espera.
- [ ] 7.2 Crear `DocMostMcp.Server.Tests/DocmostAuthHandlerTests.cs`: tests con `HttpMessageHandler` mockeado (NSubstitute) que simulan secuencia 401→200 (re-login y reintento exitoso) y 401→401 (lanza `DocmostAuthException`). Verificar que la request solo se re-envía una vez.
- [ ] 7.3 Crear `DocMostMcp.Server.Tests/DocmostToolsTests.cs`: tests parametrizados para cada tool con `HttpMessageHandler` mockeado que devuelve respuestas controladas. Verificar request body (especialmente `format: "markdown"` en create/update) y respuesta envuelta en `{ ok, statusCode, data }`.
- [ ] 7.4 Crear `DocMostMcp.Server.Tests/ConfigurationTests.cs`: tests de `DocmostOptionsValidator` con cada combinación de variables (faltantes, inválidas, válidas, puerto fuera de rango, transporte inválido).
- [ ] 7.5 Verificar que `dotnet test DocmostMcp.slnx` ejecuta todos los tests y pasan en verde.

## 8. Documentación y empaquetado

- [ ] 8.1 Reescribir `DocMostMcp.Server/README.md` con: descripción del servidor, lista de variables de entorno, ejemplo de configuración para stdio (VS Code/Copilot) y para HTTP, ejemplos de uso de cada tool, sección de troubleshooting (credenciales inválidas, puerto ocupado, sin red).
- [ ] 8.2 Verificar que `dotnet pack DocMostMcp.Server/DocMostMcp.Server.csproj -c Release` produce un `.nupkg` válido con todos los RIDs.

## 9. Validación final

- [ ] 9.1 Ejecutar `openspec validate add-docmost-mcp-server --strict` y resolver cualquier warning o error.
- [ ] 9.2 Ejecutar `dotnet build DocmostMcp.slnx -c Release` sin warnings.
- [ ] 9.3 Ejecutar `dotnet test DocmostMcp.slnx -c Release` y confirmar 100% verde.
- [ ] 9.4 Smoke test manual: arrancar el servidor en modo stdio con variables de entorno apuntando a una instancia real de Docmost, conectar con el MCP Inspector, e invocar `list_spaces` y `create_page` para confirmar end-to-end.
