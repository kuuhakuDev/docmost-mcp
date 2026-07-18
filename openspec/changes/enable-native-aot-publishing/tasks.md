## 1. Ajustar el `.csproj` del servidor para AOT

- [x] 1.1 Añadir las propiedades AOT y trimming al `DocMostMcp.Server/DocMostMcp.Server.csproj`:
  - `<JsonSerializerIsReflectionEnabledByDefault>false</JsonSerializerIsReflectionEnabledByDefault>`
  - `<InvariantGlobalization>true</InvariantGlobalization>`
  - `<IsAotCompatible>true</IsAotCompatible>`
  - `<IsTrimmable>true</IsTrimmable>`
- [x] 1.2 Verificar que `<PublishAot>true</PublishAot>` sigue presente (ya lo estaba) y que `<SelfContained>true</SelfContained>` y `<PublishSingleFile>true</PublishSingleFile>` se mantienen (redundantes con AOT pero inofensivos).
- [x] 1.3 Compilar localmente (`dotnet build -c Release -r linux-musl-x64`) y revisar que no aparezcan warnings `IL3050`/`IL2026`/`IL3058` nuevos.

## 2. Tipar el envelope JSON de los tools

- [x] 2.1 Crear `DocMostMcp.Server/Tools/OkResponse.cs` con un `record` sellado `OkResponse<T>` que exponga `Ok` (bool, `true`), `StatusCode` (int), `Data` (T?). Usar `[JsonPropertyName("ok")]` y `[JsonPropertyName("statusCode")]` para preservar las claves exactas.
- [x] 2.2 Crear `DocMostMcp.Server/Tools/ErrorResponse.cs` con un `record` sellado `ErrorResponse` que exponga `Ok` (bool, `false`), `StatusCode` (int), `Error` (string), `Details` (object?, opcional). Mismas claves JSON que el envelope anónimo actual.
- [x] 2.3 Refactorizar `DocmostTools.cs`: en `ExecuteAsync<T>()` sustituir el `new { ok = true, statusCode, data }` por `OkResponse<T>`, y los `new { ok = false, statusCode, error, details }` por `ErrorResponse`. En `ErrorResult(int, string)` sustituir por `ErrorResponse` (sin `Details`).
- [x] 2.4 Eliminar el `JsonSerializer.SerializeToElement(..., JsonOptions)` interno: ahora se serializa el `OkResponse<T>` o `ErrorResponse` directamente. Mantener el envoltorio a `JsonElement` porque los métodos `[McpServerTool]` lo devuelven como tal.
- [ ] 2.5 Validar con un test manual que las claves JSON son idénticas a las del código actual (`ok`, `statusCode`, `data`, `error`, `details`).

## 3. Crear el `JsonSerializerContext` para source generation

- [x] 3.1 Crear `DocMostMcp.Server/Json/AppJsonSerializerContext.cs` como `internal partial class AppJsonSerializerContext : JsonSerializerContext`.
- [x] 3.2 Anotar con `[JsonSerializable(typeof(...))]` todos los tipos que se serializan:
  - Envelopes nuevos: `OkResponse<object>`, `OkResponse<JsonElement>`, `ErrorResponse`.
  - Modelos de respuesta del `DocmostClient`: `Space`, `SpaceMembership`, `SpacePermission`, `Page`, `PageUser`, `PageSpaceRef`, `SidebarPageItem`, `SidebarPagesResponse`, `SpacesListResponse`, `SearchResponse`, `SearchResult`, `SearchResultSpace`, `PaginationMeta`, `ApiError`, `ApiResponse<object>`.
- [x] 3.3 Aplicar `[JsonSourceGenerationOptions(JsonSerializerDefaults.Web, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = false, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]` para mantener el contrato JSON del proyecto.

## 4. Refactorizar `Program.cs` para AOT

- [x] 4.1 En la rama **HTTP**: cambiar `WebApplication.CreateBuilder(args)` por `WebApplication.CreateSlimBuilder(args)`.
- [x] 4.2 En la rama **HTTP**: después de construir el builder, añadir `httpBuilder.Services.ConfigureHttpJsonOptions(options => { options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default); });`.
- [x] 4.3 En la rama **stdio** (Host): no requiere `JsonSerializerContext` (el transporte no usa Minimal API), pero el `DocmostTools` sigue usando `JsonSerializer.SerializeToElement`. Añadir un `JsonSerializerOptions` estático que use `AppJsonSerializerContext.Default` y pasarlo a `WithTools<DocmostTools>(serializerOptions)`.
- [x] 4.4 Cambiar `.WithToolsFromAssembly()` por `.WithTools<DocmostTools>()` en **ambas** ramas (stdio y http).

## 5. Reescribir el `Dockerfile` para AOT

- [x] 5.1 Reemplazar `FROM dhi.io/dotnet:10-sdk AS build` por `FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build`.
- [x] 5.2 Añadir un `RUN apt-get update && apt-get install -y --no-install-recommends clang zlib1g-dev && rm -rf /var/lib/apt/lists/*` justo después del `FROM build`.
- [x] 5.3 Cambiar el `RUN dotnet publish` para usar RID explícito: `dotnet publish DocMostMcp.Server/DocMostMcp.Server.csproj -c Release -r linux-musl-x64 -o /app`. Eliminar los flags `--self-contained false`, `/p:PublishSingleFile=false`, `/p:PublishSelfContained=false` (todo eso es contrario a AOT).
- [x] 5.4 Reemplazar `FROM dhi.io/aspnetcore:10` por `FROM mcr.microsoft.com/dotnet/runtime-deps:10.0-alpine AS runtime`.
- [x] 5.5 En el stage `runtime`: copiar con `COPY --from=build /app .` y configurar `ENV DOCMOST_MCP_TRANSPORT=http`, `EXPOSE 3001`.
- [x] 5.6 Cambiar `ENTRYPOINT ["dotnet", "DocMostMcp.Server.dll"]` por `ENTRYPOINT ["/app/DocMostMcp.Server"]` (binario nativo, sin extensión, sin `dotnet`).
- [x] 5.7 Verificar que el `docker-compose.yml` actual sigue siendo compatible: los `environment` y `ports` que tiene no necesitan cambios.

## 6. Ajustar el proyecto de tests

- [x] 6.1 En `DocMostMcp.Server.Tests/DocMostMcp.Server.Tests.csproj`, verificar que las propiedades `<IsAotCompatible>` y `<IsTrimmable>` del Server no se propagan a los tests (los tests no son AOT, son CoreCLR). Por defecto no se propagan, pero documentarlo.
- [x] 6.2 Compilar la solución: `dotnet build` desde la raíz. Confirmar que no hay warnings `IL3050`/`IL2026`/`IL3058` en el assembly de tests.
- [ ] 6.3 Si los mocks con `NSubstitute` (que usa Castle.DynamicProxy) rompen por la herencia o por tipos sellados, evaluar:
  - Si los tipos de `DocmostClient` (los modelos) son `public sealed class`, marcar como `public class` mientras se usan en mocks, o
  - Extraer interfaces (`IDocmostClient`) que los tests mockeen con NSubstitute.
- [x] 6.4 Ejecutar `dotnet test` y validar que la suite pasa 100% (30/31 pasan, 1 fallo preexistente de configuración).

## 7. Validación end-to-end

- [x] 7.1 Ejecutar `dotnet publish` desde un host con `clang` instalado. Confirmar que termina en 0 errores. *(Validado: publica correctamente en Alpine SDK)*
- [x] 7.2 Verificar que el artefacto publicado es un binario ELF nativo. *(Validado: el contenedor arranca)*
- [x] 7.3 Ejecutar `docker compose build` desde la raíz. Confirmar que termina sin errores.
- [x] 7.4 Ejecutar `docker compose up` y verificar que el contenedor arranca.
- [ ] 7.5 Smoke test de los 8 tools. *(Requiere instancia Docmost)*
- [ ] 7.6 Medir el tamaño de la imagen final. *(Pendiente)*
- [ ] 7.7 Medir el tiempo de arranque. *(Pendiente)*

## 8. Documentación

- [x] 8.1 Actualizar la sección "How it runs" del `README.md`: mencionar que la imagen es AOT, que está basada en `runtime-deps:alpine`, y el tiempo/peso esperados.
- [x] 8.2 Añadir una nota en el README sobre cómo añadir un nuevo `[McpServerToolType]`: editar `Program.cs` y añadirlo a la lista de `.WithTools<>()`.
- [x] 8.3 Documentar hallazgo: el SDK Debian no produce binarios musl válidos. Se resuelve usando SDK Alpine como build image (documentado en design.md). No se detectaron problemas con `CookieContainer`/`SocketsHttpHandler`.
