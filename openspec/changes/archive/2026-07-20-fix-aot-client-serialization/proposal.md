## Why

El cambio `2026-07-18-enable-native-aot-publishing` (archivado) habilitó la infraestructura AOT del servidor: añadió los flags `<PublishAot>true</PublishAot>` y `<JsonSerializerIsReflectionEnabledByDefault>false</JsonSerializerIsReflectionEnabledByDefault>` al `.csproj`, reescribió el `Dockerfile` para producir un binario nativo ELF basado en `runtime-deps:alpine`, tipó los envelopes JSON en `OkResponse<T>` y `ErrorResponse`, y registró un `AppJsonSerializerContext` parcial para los tipos de salida. **Pero dejó la mitad del trabajo sin hacer**: la capa de cliente HTTP que habla con Docmost (`DocmostClient` y `CookieSessionStore`) sigue usando `JsonSerializerOptions` sin `TypeInfoResolver` y los overloads AOT-incompatibles de `HttpClient.PostAsJsonAsync` / `HttpContent.ReadFromJsonAsync`. El binario AOT arranca, pero en cuanto cualquier tool ejecuta una llamada real a Docmost, salta `InvalidOperationException("Reflection-based serialization has been disabled...")`, que el handler atrapa y devuelve como `401` espurio en la respuesta del tool.

El spec `openspec/specs/native-aot-publishing/spec.md` ya define como **MUST** que *"toda la serialización y deserialización JSON en el servidor MUST usar `System.Text.Json` con source generators, declarados en un `JsonSerializerContext` parcial, sin recurrir a reflexión en runtime"*. El cambio actual es **completar la implementación** de ese requisito en la capa cliente, no introducir un requisito nuevo.

Adicionalmente, el path del transporte HTTP streamable se sirve actualmente en `/` (porque `Program.cs` hace `app.MapMcp()` sin patrón), lo que es un anti-patrón: choca con cualquier futuro endpoint de health-check o de UI y no se alinea con la convención habitual del ecosistema MCP. El cambio también lo mueve a `/mcp`, que es el path recomendado por la documentación oficial del SDK.

## What Changes

- **Completar `AppJsonSerializerContext`** (`DocMostMcp.Server/Json/AppJsonSerializerContext.cs`): añadir `[JsonSerializable]` para los tipos que cruzan la frontera de serialización en la capa cliente y que faltan en el catálogo actual — `string[]` (mensajes de error de Docmost que son arrays), `object` (miembros `Details` de `ErrorResponse` y `Message` de `ApiError`, ambos declarados como `object?`), `LoginRequest` (body de `POST /api/auth/login`), `Dictionary<string, object>` (los bodies de los endpoints de Docmost se construyen como diccionarios anónimos), `byte[]` (contenido crudo en `DocmostAuthHandler.CloneHttpRequestMessageAsync`).
- **Refactorizar `DocmostClient`** (`DocMostMcp.Server/Client/DocmostClient.cs`): eliminar el `JsonSerializerOptions` estático sin `TypeInfoResolver` y sustituirlo por `AppJsonSerializerContext.Default.Options`. Cambiar las 3 llamadas a `PostAsJsonAsync(endpoint, body, JsonOptions, ct)` y `JsonSerializer.Deserialize<T>(json, JsonOptions)` por los overloads AOT-safe que aceptan `JsonTypeInfo<T>` o `JsonSerializerContext` (los marcados **sin** `[RequiresDynamicCode]` / `[RequiresUnreferencedCode]` por la documentación oficial de Microsoft).
- **Refactorizar `CookieSessionStore`** (`DocMostMcp.Server/Client/CookieSessionStore.cs`): pasar `AppJsonSerializerContext.Default` (o los `JsonTypeInfo<T>` correspondientes) a las dos llamadas a `PostAsJsonAsync` y `ReadFromJsonAsync<ApiError>` que hoy se ejecutan sin opciones — y que por tanto usan el `JsonSerializerOptions` por defecto, que requiere reflexión.
- **Arreglar `ApiError.FromJsonElement`** (`DocMostMcp.Server/Client/Models/ApiError.cs`): la rama del `switch` que deserializa un `string[]` desde `msgProp.GetRawText()` no pasa el contexto; añadir `AppJsonSerializerContext.Default.StringArray` (o el nombre equivalente) para que el source generator emita el código.
- **Mover el endpoint HTTP de MCP a `/mcp`** (`DocMostMcp.Server/Program.cs`): cambiar `app.MapMcp()` por `app.MapMcp("/mcp")` en la rama HTTP. Documentar el path como MUST en el spec `mcp-transport`. Actualizar el `README.md` para reflejar la nueva URL.
- **Encadenar `McpJsonUtilities` con `AppJsonSerializerContext`** en el `TypeInfoResolverChain` del `ConfigureHttpJsonOptions` (rama HTTP de `Program.cs`): según la documentación oficial del SDK de MCP, cuando se usa un `JsonSerializerContext` custom junto con el transporte HTTP, hay que añadir el resolver del SDK (`McpJsonUtilities.DefaultOptions.TypeInfoResolver!`) **primero** en la cadena, para que los tipos internos del protocolo MCP (`JsonRpcRequest`, `JsonRpcResponse`, `CallToolRequest`, etc.) tengan contrato cuando el SDK los serializa.
- **Actualizar el `README.md`** para reflejar: (a) la URL correcta del endpoint HTTP (`http://localhost:3001/mcp`), (b) nota de troubleshooting si alguien apunta a la URL antigua.
- **Actualizar el spec `mcp-transport`** para añadir un requisito MUST explícito: *"el endpoint HTTP streamable DEBE servirse en el path `/mcp`"*, con escenarios que cubran tanto la ruta absoluta como el método HTTP (`POST` para invocaciones, `GET`/`DELETE` para sesiones streamable).

## Capabilities

### New Capabilities

<!-- No aplica. No se introduce ninguna capacidad nueva. Se completan dos capacidades
     existentes (`native-aot-publishing` y `mcp-transport`) que ya estaban definidas
     pero cuya implementación estaba parcial. -->

### Modified Capabilities

- `native-aot-publishing`: el escenario *"All tool envelopes are typed"* ya estaba cumplido en la capa de transporte (MCP), pero el spec dice "toda la serialización y deserialización JSON en el servidor". Esto añade el delta de la capa cliente, que era la zona que faltaba.
- `mcp-transport`: se añade un requisito nuevo `Path del endpoint HTTP streamable` con el MUST de servirse en `/mcp`. Se mantiene la compatibilidad del comportamiento de auto-detección de transporte (stdio vs http) sin cambios.

## Impact

**Archivos modificados:**

- `DocMostMcp.Server/Json/AppJsonSerializerContext.cs` — 6 nuevos `[JsonSerializable]`.
- `DocMostMcp.Server/Client/DocmostClient.cs` — eliminar `JsonOptions` estático, refactor de 3 puntos de uso a overloads AOT-safe.
- `DocMostMcp.Server/Client/CookieSessionStore.cs` — pasar `JsonTypeInfo<T>` o `JsonSerializerContext` a 2 llamadas de extensión HTTP.
- `DocMostMcp.Server/Client/Models/ApiError.cs` — usar `JsonTypeInfo<string[]>` en `FromJsonElement`.
- `DocMostMcp.Server/Program.cs` — `app.MapMcp("/mcp")` y encadenar `McpJsonUtilities.DefaultOptions.TypeInfoResolver!` en el `TypeInfoResolverChain` de la rama HTTP.
- `README.md` — URL correcta del endpoint, nota de troubleshooting.
- `openspec/specs/native-aot-publishing/spec.md` — nuevo escenario que cubre la capa cliente.
- `openspec/specs/mcp-transport/spec.md` — nuevo requisito `Path del endpoint HTTP streamable` con escenarios.

**Archivos no tocados:**

- `.csproj` (los flags AOT ya están, no se añaden ni se quitan).
- `Dockerfile` (la imagen AOT ya es correcta).
- `DocmostAuthHandler.cs` (no hace serialización, sólo lee bytes).
- `DocMostMcp.Server.Tests/` (no requiere cambios — los tests compilan en CoreCLR).
- API pública MCP: los 8 tools siguen exponiendo el mismo esquema, los mismos nombres y el mismo envelope JSON.
- Variables de entorno y semántica de auth (mismos reintentos, misma cookie store, misma serialización de errores).
- `docker-compose.yml` (no le afecta el path del endpoint).

**Riesgo de regresión:** bajo. El cambio de path `/` → `/mcp` es observable para cualquier cliente MCP que ya apuntara a `http://host:port/`. Como el proyecto está en `0.1.0-beta` y el transporte HTTP es todavía experimental, no hay clientes en producción apuntando a la URL antigua. El refactor de serialización no cambia el contrato JSON: mismos envelopes, mismos códigos de estado, misma semántica de errores.

**Validación previa (de la documentación oficial):**

- Microsoft Learn — *How to use source generation in System.Text.Json*: "use overloads of HttpClientJsonExtensions.GetFromJsonAsync and HttpClientJsonExtensions.PostAsJsonAsync extension methods that take a source generation context or **TypeInfo<TValue>**" — el overload que acepta `JsonSerializerOptions` está marcado con `[RequiresDynamicCode]` y `[RequiresUnreferencedCode]`, por lo que rompe el publish AOT.
- Microsoft Learn — *ASP.NET Core support for Native AOT*: el patrón `TypeInfoResolverChain.Insert(0, MyContext.Default)` es exactamente el que ya usa el proyecto, sólo le falta el resolver del SDK de MCP antepuesto.
- MCP C# SDK — *docs/experimental.md*: *"Configure JsonSerializerOptions to include the SDK's resolver first in the TypeInfoResolverChain"*. Es la fuente que justifica encadenar `McpJsonUtilities.DefaultOptions.TypeInfoResolver!` con nuestro `AppJsonSerializerContext`.
- MCP C# SDK — *docs/concepts/transports/transports.md*: el patrón `app.MapMcp("/mcp")` es la forma soportada de fijar el path del endpoint.
