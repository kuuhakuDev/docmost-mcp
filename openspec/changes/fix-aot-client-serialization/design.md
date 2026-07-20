## Context

El proyecto `docmost-mcp` es un servidor MCP para [Docmost](https://docmost.com) sobre .NET 10, con el SDK `ModelContextProtocol.AspNetCore 1.2.0`, publicado como binario nativo AOT para `linux-musl-x64`. El cambio anterior (`2026-07-18-enable-native-aot-publishing`) alineó el `.csproj`, el `Dockerfile` y la capa de transporte MCP con AOT, pero la capa de **cliente HTTP que habla con Docmost** quedó usando APIs AOT-incompatibles.

**Estado actual problemático (lo que rompe el binario AOT en runtime):**

1. `DocmostClient.cs` (líneas 15-19) declara un `JsonSerializerOptions JsonOptions` estático **sin** `TypeInfoResolver`. Se usa en 3 puntos: `PostAsJsonAsync(endpoint, body, JsonOptions, ct)` (línea 197), `JsonSerializer.Deserialize<T>(rawText, JsonOptions)` (líneas 225 y 244). Los tres rompen en AOT porque el resolver por defecto está deshabilitado por `JsonSerializerIsReflectionEnabledByDefault=false`.
2. `CookieSessionStore.cs` invoca `_httpClient.PostAsJsonAsync("/api/auth/login", request, cancellationToken)` (línea 126) y `response.Content.ReadFromJsonAsync<ApiError>(cancellationToken: cancellationToken)` (línea 155) **sin pasar opciones**. En `JsonSerializerIsReflectionEnabledByDefault=false`, estos overloads sin opciones también fallan.
3. `ApiError.FromJsonElement` (línea 44) hace `JsonSerializer.Deserialize<string[]>(msgProp.GetRawText())` sin contexto. `string[]` no está registrado en el `AppJsonSerializerContext` actual.
4. `AppJsonSerializerContext` no incluye los tipos que cruzan la frontera: `string[]`, `object`, `LoginRequest`, `Dictionary<string, object>`, `byte[]`. (El `byte[]` aparece cuando `DocmostAuthHandler` clona el `HttpRequestMessage` para reintentos: `request.Content.ReadAsByteArrayAsync(...)` — ese array luego se re-serializa como `ByteArrayContent`, no como JSON, pero al pasarlo a través de `new ByteArrayContent(contentBytes)` no requiere `[JsonSerializable]`. Lo dejo documentado por si en el futuro alguien intenta serializar el array de bytes a JSON.)
5. `Program.cs` (línea 83) hace `app.MapMcp()` sin patrón → el endpoint HTTP streamable se sirve en `/`. Esto choca con la convención del ecosistema MCP y bloquea futuros endpoints de health/métricas.
6. `Program.cs` (línea 72) hace `TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default)` pero **no** antepone `McpJsonUtilities.DefaultOptions.TypeInfoResolver!`. Esto significa que los tipos internos del protocolo MCP (`JsonRpcRequest`, `JsonRpcResponse`, `CallToolRequest`, `ListToolsRequest`, etc.) no tienen un `JsonTypeInfo` explícito cuando el SDK los serializa dentro del transporte HTTP. En modo HTTP por defecto el SDK funciona, pero las propiedades experimentales del protocolo (futuras versiones, capabilities nuevos) pueden fallar.

**Lo que sí está bien (no se toca):**

- El `AppJsonSerializerContext` ya tiene los 14 tipos de modelo (`Space`, `Page`, `SearchResult`, etc.) y los envelopes (`OkResponse<T>`, `ErrorResponse`) registrados correctamente.
- `Program.cs` ya pasa `AppJsonSerializerContext.Default.Options` al `WithTools<DocmostTools>` en la rama stdio.
- `OkResponse<T>` y `ErrorResponse` ya están tipados como `record` sellado.
- `DocmostAuthHandler` no usa JSON; sólo manipula cookies y headers HTTP.
- El Dockerfile ya produce una imagen `runtime-deps:alpine` con un binario nativo ELF, arrancado directamente sin `dotnet`.

## Goals / Non-Goals

**Goals:**

- Eliminar todo uso de `JsonSerializer` con reflexión en el runtime del binario AOT publicado.
- Mantener intacta la API pública de los 8 tools (mismo esquema, mismos nombres, mismo envelope JSON).
- Mover el endpoint HTTP streamable a `/mcp` sin cambiar el resto del routing.
- Encadenar el resolver de tipos del SDK de MCP con el `AppJsonSerializerContext` del proyecto, siguiendo la recomendación oficial.
- Compilar el proyecto en AOT sin warnings `IL2026` / `IL3050` / `IL3058` nuevos.
- Que los 8 tools funcionen end-to-end contra una instancia real de Docmost con el binario AOT publicado.

**Non-Goals:**

- Reescribir la lógica de `DocmostClient` o `CookieSessionStore` (siguen siendo lo que son: wrappers HTTP tipados).
- Cambiar el modelo de auth (cookie de sesión, reintento en 401, marcado terminal). Es exactamente el mismo.
- Cambiar la forma en que se construye el body de los POST a Docmost (`Dictionary<string, object>`). Es un detalle interno de `DocmostClient` que no afecta al contrato.
- Añadir un endpoint de health-check en `/health` o un dashboard. Queda para un cambio futuro.
- Versionado semántico / release: este cambio no es un release per se, sólo completa una feature del spec.
- Mover el path `/mcp` a un subdominio (no aplica, es localhost / container).

## Decisions

### Decisión 1 — Usar los overloads AOT-safe de `HttpClientJsonExtensions`

**Por qué:** La [documentación oficial de Microsoft](https://learn.microsoft.com/dotnet/standard/serialization/system-text-json/source-generation) lo recomienda explícitamente para AOT, y el overload que acepta `JsonSerializerOptions` está marcado con `[RequiresDynamicCode]` y `[RequiresUnreferencedCode]` — lo que rompe el publish AOT.

**Cómo se aplica:**

```csharp
// ❌ Antes (AOT-incompatible, marcado con [RequiresDynamicCode])
await _httpClient.PostAsJsonAsync(endpoint, body, JsonOptions, ct);
JsonSerializer.Deserialize<T>(rawText, JsonOptions);

// ✅ Después (AOT-safe)
await _httpClient.PostAsJsonAsync<Dictionary<string, object>>(
    endpoint, body, AppJsonSerializerContext.Default.DictionaryStringObject, ct);
JsonSerializer.Deserialize(rawText, AppJsonSerializerContext.Default.SpacesListResponse);
```

El cambio de `Dictionary<string, object>` a un tipo específico se hace porque (a) el source generator necesita un tipo raíz conocido en tiempo de compilación, y (b) en la práctica `body` siempre se construye con un `Dictionary<string, object?>` dentro de cada método (`ListSpacesAsync`, `GetSpaceInfoAsync`, etc.).

**Validación:** La firma `PostAsJsonAsync<TValue>(this HttpClient, string?, TValue, JsonTypeInfo<TValue>, CancellationToken)` está documentada en `learn.microsoft.com/dotnet/api/system.net.http.json.httpclientjsonextensions.postasjsonasync` y no tiene los atributos `Requires*`.

### Decisión 2 — Añadir 5 `[JsonSerializable]` al context

**Por qué:** El source generator sólo emite código para tipos raíz explícitamente declarados (más los tipos transitivos de sus propiedades, con la excepción de `object` que requiere declaración explícita). Los tipos que faltan son:

| Tipo | Por qué hace falta | Dónde se usa |
|---|---|---|
| `string[]` | `ApiError.FromJsonElement` deserializa arrays de strings cuando Docmost devuelve `message: [...]` | `Client/Models/ApiError.cs:44` |
| `object` | `ErrorResponse.Details` y `ApiError.Message` están declarados como `object?`; la documentación oficial advierte que los miembros `object` requieren declaración explícita para serialización polimórfica | `Tools/ErrorResponse.cs`, `Client/Models/ApiError.cs` |
| `LoginRequest` | Body de `POST /api/auth/login` | `Client/CookieSessionStore.cs:126` |
| `Dictionary<string, object>` | Body de los 8 endpoints de Docmost | `Client/DocmostClient.cs:40, 56, 72, 92, 112, 135, 154, 172` |
| `JsonElement` | Ya está como `OkResponse<JsonElement>`, pero algunos paths internos lo referencian suelto | `Tools/DocmostTools.cs:243` (vía `OkResponse<T?>`) |

**Por qué no `[JsonSerializable(typeof(object))]` resuelve lo de `ErrorResponse.Details`:** Sí, lo resuelve. La regla "members declared as object are an exception" aplica porque el tipo runtime concreto no es conocido en tiempo de compilación — pero como el context sólo necesita **poder serializar** un `object?` (no deserializar a un tipo polimórfico concreto), basta con que `object` esté registrado como tipo raíz. El source generator emite el `JsonTypeInfo<object>` que sabe serializar cualquier JSON value primitivo (string, number, bool, null, array, object).

**Validación:** Documentación oficial de Microsoft: *"Members declared as `object` are an exception to this rule. The runtime type for a member declared as `object` needs to be specified."* — confirma que `object` debe estar en el context.

### Decisión 3 — Encadenar `McpJsonUtilities.DefaultOptions.TypeInfoResolver!` con `AppJsonSerializerContext.Default`

**Por qué:** La [documentación oficial del SDK de MCP](https://github.com/modelcontextprotocol/csharp-sdk/blob/main/docs/experimental.md) lo recomienda: *"Configure JsonSerializerOptions to include the SDK's resolver first in the TypeInfoResolverChain. This ensures MCP types, including experimental properties, are handled correctly by the SDK's contract, even when using custom source-generated serialization contexts."*

**Orden de la cadena:**

```csharp
options.SerializerOptions.TypeInfoResolverChain.Insert(0, McpJsonUtilities.DefaultOptions.TypeInfoResolver!);
options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
```

El de MCP **primero** (al frente) porque contiene los contratos de los tipos del protocolo MCP. El del proyecto **segundo** para que nuestros tipos tengan prioridad cuando hay coincidencia.

**Por qué no usar `JsonTypeInfoResolver.Combine(...)`:** El `Insert(0, ...)` es la API mutable recomendada en .NET 8+ para encadenar contextos dinámicamente. `Combine` congela la cadena en tiempo de construcción, lo que es menos flexible si en el futuro el SDK añade nuevos tipos vía assembly part.

### Decisión 4 — `app.MapMcp("/mcp")` (path absoluto, no prefijo)

**Por qué:** La [documentación oficial del SDK](https://github.com/modelcontextprotocol/csharp-sdk/blob/main/docs/concepts/transports/transports.md) muestra el patrón canónico: `app.MapMcp("/mcp")`. Es el path que la mayoría de clientes MCP esperan, y deja libre la raíz para futuros endpoints de health o UI.

**Por qué no `app.MapMcp("/mcp/")` (con slash final):** ASP.NET Core normaliza las rutas, pero el path canónico en la documentación no lleva slash. Mantener el patrón simple evita ambigüedades en el routing de ASP.NET Core (un endpoint con slash final puede o no hacer redirect).

**Por qué no `/{category?}` (ruta con parámetro):** No aplica a este caso. La per-session configuration es un feature del SDK que no usamos; sólo necesitamos un path fijo.

**Impacto para los clientes MCP:** Cualquier cliente que se conecte a `http://host:port/` debe cambiar a `http://host:port/mcp`. El README documenta el nuevo path.

### Decisión 5 — No tocar `AppJsonSerializerContext.Default.Options` en `Program.cs` rama stdio

**Por qué:** La rama stdio ya pasa `new JsonSerializerOptions(AppJsonSerializerContext.Default.Options)` al `WithTools<DocmostTools>(stdioSerializerOptions)`. El constructor copia el `TypeInfoResolver` por referencia, así que el chain se preserva. No hace falta tocar nada en esa rama.

**Validación:** El código actual (línea 56) es: `var stdioSerializerOptions = new JsonSerializerOptions(AppJsonSerializerContext.Default.Options);` — la copia se hace por valor de las opciones pero el resolver queda apuntando al mismo `AppJsonSerializerContext.Default`. Confirmado leyendo la API de `JsonSerializerOptions` (los `TypeInfoResolver` y `TypeInfoResolverChain` son inmutables una vez creado el context, no se duplican).

## Risks & Mitigations

**Riesgo 1: `Dictionary<string, object>` puede no serializarse como espera Docmost.**

- **Descripción:** El body que se envía a Docmost tiene claves como `spaceId`, `query`, `limit`, `cursor`. El source generator los serializa con camelCase (por `PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase` en el `JsonSourceGenerationOptions`). Como `Dictionary<string, object>` no es un tipo con propiedades, el `PropertyNamingPolicy` **no se aplica** a sus claves — la serialización respeta las claves literales (`"spaceId"`, `"query"`, etc.). El comportamiento es idéntico al código actual.
- **Mitigación:** Verificación con test de humo contra una instancia real (task 5.2 del tasks.md).

**Riesgo 2: El cambio de path `/` → `/mcp` rompe clientes existentes.**

- **Descripción:** Cualquier cliente que se conecte a `http://host:port/` recibe un 404 después del cambio.
- **Mitigación:** (a) Documentar el cambio prominentemente en el README (sección Usage > HTTP mode). (b) El proyecto está en `0.1.0-beta`; no hay clientes en producción. (c) Si alguien tiene scripts automatizados contra la URL antigua, el cambio les obliga a actualizarse a la vez que adoptan la versión AOT-publicada del binario.

**Riesgo 3: `McpJsonUtilities.DefaultOptions.TypeInfoResolver!` requiere una versión concreta del SDK.**

- **Descripción:** El resolver es `internal` o `public` dependiendo de la versión. Si la API cambia en versiones futuras, hay que actualizar.
- **Mitigación:** La versión del SDK está pinneada en el `.csproj` (`ModelContextProtocol.AspNetcore 1.2.0`). El type `McpJsonUtilities` es público en esa versión (verificado en la [documentación de la API](https://csharp.sdk.modelcontextprotocol.io/api/ModelContextProtocol.McpJsonUtilities.html)).

**Riesgo 4: `ErrorResponse.Details` como `object?` serializa con metadata extra de tipo.**

- **Descripción:** El source generator emite un `JsonTypeInfo<object>` que serializa el valor con un wrapper de tipo polimórfico. El formato JSON resultante puede no coincidir con la salida actual (que era un `new { ok, statusCode, error, details = (object?)message }` anónimo).
- **Mitigación:** La doc del envelope `ErrorResponse` actual dice explícitamente *"Serialized with the same keys as the previous anonymous-type envelope to maintain backward compatibility"*. El tipo `object?` se serializa con el source generator como un JSON value estándar (string, array, etc.) — sin wrapper de tipo. El shape es idéntico.

## Migration Plan

1. **Branch:** `feature/aot-client-serialization` desde `develop` (creada).
2. **Implementación:** Siguiendo `tasks.md` (1-9). Cada task tiene criterios de aceptación verificables.
3. **Validación local:** `dotnet build -c Release` + `dotnet test` desde la raíz. Confirmar 0 warnings `IL*` nuevos.
4. **Validación AOT:** `dotnet publish -c Release -r linux-musl-x64` desde el SDK Alpine. Confirmar 0 warnings `IL*` nuevos.
5. **Validación end-to-end:** `docker compose up` + smoke test de los 8 tools contra una instancia de Docmost en `localhost:3000`.
6. **PR a develop** con título `fix(aot): complete source-gen serialization in client layer + serve HTTP at /mcp`.
7. **Merge con `--no-ff`** para preservar la historia de la rama.
8. **Archive del change OpenSpec** tras el merge.

## Open Questions

- ¿Hay que añadir un endpoint `GET /health` para monitoring? **No en este cambio** (fuera de scope, queda para `feature/mcp-health-endpoint`).
- ¿La forma del `Details` (object?) puede traer problemas con clientes MCP que esperan un string concreto? **Verificar en smoke test** — si rompe, se cambia `Details` a `string?` y se serializa con `JsonSerializer.Serialize(details)`. Bajo riesgo según el uso actual (`result.Error?.Message` se le pasa a `Details`, y `Message` ya es `object?`).
