## 1. Completar `AppJsonSerializerContext` con los tipos faltantes

- [ ] 1.1 En `DocMostMcp.Server/Json/AppJsonSerializerContext.cs`, añadir los siguientes `[JsonSerializable(typeof(...))]`:
  - `[JsonSerializable(typeof(string[]))]` — para deserializar arrays de strings en `ApiError.FromJsonElement`.
  - `[JsonSerializable(typeof(object))]` — para serializar miembros `object?` polimórficos (`ErrorResponse.Details`, `ApiError.Message`). Documentado por Microsoft como tipo que **requiere** declaración explícita cuando aparece como miembro.
  - `[JsonSerializable(typeof(LoginRequest))]` — para el body de `POST /api/auth/login`.
  - `[JsonSerializable(typeof(Dictionary<string, object>))]` — para los bodies que `DocmostClient` construye como `Dictionary<string, object?>`.
  - `[JsonSerializable(typeof(OkResponse<Dictionary<string, object>>))]` — necesario porque las tools pueden serializar envelopes cuyo `Data` es un diccionario (aunque hoy no se usa, el source generator puede quejarse si no está).
  - `[JsonSerializable(typeof(JsonElement))]` — algunos paths internos referencian `JsonElement` suelto (no solo dentro de `OkResponse<JsonElement>`).
- [ ] 1.2 Compilar con `dotnet build -c Release` desde la raíz. Verificar que **no** aparecen nuevos warnings `IL2026` / `IL3050` / `IL3058`.
- [ ] 1.3 Si el source generator se queja de tipos faltantes (puede pasar al añadir `object`), iterar añadiendo los tipos que reporte (puede ser necesario `JsonNode`, `JsonObject`, `JsonArray`, `JsonValue`).

## 2. Refactorizar `DocmostClient` para usar overloads AOT-safe

- [ ] 2.1 En `DocMostMcp.Server/Client/DocmostClient.cs`, eliminar el campo `private static readonly JsonSerializerOptions JsonOptions = new() { ... }` (líneas 15-19).
- [ ] 2.2 En el método privado `PostAsync<T>` (línea 197), cambiar la llamada a `PostAsJsonAsync`:
  - **Antes:** `await _httpClient.PostAsJsonAsync(endpoint, body, JsonOptions, cancellationToken);`
  - **Después:** `await _httpClient.PostAsJsonAsync(endpoint, body, AppJsonSerializerContext.Default.DictionaryStringObject, cancellationToken);`
  - Esto usa el overload `PostAsJsonAsync<TValue>(this HttpClient, string?, TValue, JsonTypeInfo<TValue>, CancellationToken)` que está **sin** atributos `RequiresDynamicCode` / `RequiresUnreferencedCode`.
- [ ] 2.3 En la misma `PostAsync<T>` (línea 225), cambiar la deserialización del campo `data`:
  - **Antes:** `data = JsonSerializer.Deserialize<T>(dataProp.GetRawText(), JsonOptions);`
  - **Después:** usar `JsonSerializer.Deserialize(dataProp.GetRawText(), AppJsonSerializerContext.Default.GetTypeInfoForT<T>())`. **Problema:** `T` es genérico, el source generator no emite un `JsonTypeInfo<T>` "abierto". Solución: usar `JsonSerializer.Deserialize(json, (JsonTypeInfo)AppJsonSerializerContext.Default.GetTypeInfo(typeof(T))!)` — el `JsonSerializerContext` base tiene un `GetTypeInfo(Type)` que devuelve el `JsonTypeInfo` del tipo concreto si está registrado. Si `T` no está registrado, lanzar `InvalidOperationException` con mensaje claro ("Type {T} is not registered in AppJsonSerializerContext. Add [JsonSerializable(typeof(T))] to the context.").
  - **Alternativa más limpia:** Usar `JsonSerializer.Deserialize(dataProp.GetRawText(), AppJsonSerializerContext.Default.Options)` — el `Options` del context ya tiene el `TypeInfoResolver` configurado, así que la búsqueda de `JsonTypeInfo<T>` funciona en runtime **si** `T` está registrado. Verificar con smoke test.
  - **Decisión final:** ir con la **alternativa más limpia** (usar `AppJsonSerializerContext.Default.Options` como options, no `JsonOptions`). El source generator emite todos los `JsonTypeInfo` que aparecen en los `[JsonSerializable]`, y la búsqueda en runtime funciona transparentemente.
- [ ] 2.4 En la misma `PostAsync<T>` (línea 244), cambiar la deserialización de la respuesta completa:
  - **Antes:** `var data = JsonSerializer.Deserialize<T>(responseBody, JsonOptions);`
  - **Después:** `var data = JsonSerializer.Deserialize(responseBody, AppJsonSerializerContext.Default.Options) as T;`
  - Mismo rationale que 2.3.
- [ ] 2.5 Eliminar el `using System.Text.Json;` si deja de ser necesario (el `System.Text.Json.Serialization` ya viene por implicit usings; verificar).
- [ ] 2.6 Compilar y verificar 0 warnings nuevos.

## 3. Refactorizar `CookieSessionStore` para usar overloads AOT-safe

- [ ] 3.1 En `DocMostMcp.Server/Client/CookieSessionStore.cs`, añadir `using DocMostMcp.Server.Json;` (si no está ya).
- [ ] 3.2 En el método `LoginAsync` (línea 126), cambiar la llamada a `PostAsJsonAsync`:
  - **Antes:** `await _httpClient.PostAsJsonAsync("/api/auth/login", request, cancellationToken);`
  - **Después:** `await _httpClient.PostAsJsonAsync("/api/auth/login", request, AppJsonSerializerContext.Default.LoginRequest, cancellationToken);`
- [ ] 3.3 En el mismo `LoginAsync` (línea 155), cambiar la llamada a `ReadFromJsonAsync`:
  - **Antes:** `var body = await response.Content.ReadFromJsonAsync<ApiError>(cancellationToken: cancellationToken);`
  - **Después:** `var body = await response.Content.ReadFromJsonAsync(AppJsonSerializerContext.Default.ApiError, cancellationToken);`
- [ ] 3.4 Compilar y verificar 0 warnings nuevos.

## 4. Arreglar `ApiError.FromJsonElement` para usar JsonTypeInfo

- [ ] 4.1 En `DocMostMcp.Server/Client/Models/ApiError.cs`, añadir `using DocMostMcp.Server.Json;` (si no está ya).
- [ ] 4.2 En `FromJsonElement` (línea 44), cambiar la deserialización del array:
  - **Antes:** `JsonValueKind.Array => JsonSerializer.Deserialize<string[]>(msgProp.GetRawText()),`
  - **Después:** `JsonValueKind.Array => JsonSerializer.Deserialize(msgProp.GetRawText(), AppJsonSerializerContext.Default.StringArray),`
- [ ] 4.3 Compilar y verificar 0 warnings nuevos.

## 5. Mover el endpoint HTTP a `/mcp`

- [ ] 5.1 En `DocMostMcp.Server/Program.cs` (línea 83), cambiar `app.MapMcp();` por `app.MapMcp("/mcp");`.
- [ ] 5.2 Encadenar el resolver del SDK de MCP en el `TypeInfoResolverChain` (líneas 70-73):
  - **Antes:**
    ```csharp
    httpBuilder.Services.ConfigureHttpJsonOptions(options =>
    {
        options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
    });
    ```
  - **Después:**
    ```csharp
    using ModelContextProtocol; // para McpJsonUtilities

    httpBuilder.Services.ConfigureHttpJsonOptions(options =>
    {
        options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
        options.SerializerOptions.TypeInfoResolverChain.Insert(0, McpJsonUtilities.DefaultOptions.TypeInfoResolver!);
    });
    ```
  - Verificar el orden: el resolver del SDK queda **primero** (al frente) en la cadena, el del proyecto segundo. Insertar en posición 0 prepende, así que hay que insertar el del proyecto **primero** (queda detrás) y luego el del SDK (queda al frente). El código anterior ya está en el orden correcto.
- [ ] 5.3 Compilar y verificar 0 warnings nuevos.
- [ ] 5.4 Verificar que `McpJsonUtilities.DefaultOptions` es público en `ModelContextProtocol.AspNetCore 1.2.0`. Si no lo es, probar `ModelContextProtocol.McpJsonUtilities.DefaultOptions` o el namespace `ModelContextProtocol.Utils.Json.McpJsonUtilities` (según la versión exacta del SDK).

## 6. Validación con build AOT

- [ ] 6.1 Publicar con AOT: `dotnet publish DocMostMcp.Server/DocMostMcp.Server.csproj -c Release -r linux-musl-x64 -o /tmp/aot-test` (desde un host con el SDK Alpine o con `clang` + `zlib1g-dev` instalados).
- [ ] 6.2 Verificar que el comando termina con código 0.
- [ ] 6.3 Verificar que el log no contiene warnings `IL2026` / `IL3050` / `IL3058` nuevos.
- [ ] 6.4 Verificar que el artefacto en `/tmp/aot-test/DocMostMcp.Server` es un binario ELF: `file /tmp/aot-test/DocMostMcp.Server` debe reportar `ELF 64-bit LSB pie executable, x86-64`.

## 7. Validación con docker compose

- [ ] 7.1 Construir la imagen: `docker compose build`.
- [ ] 7.2 Arrancar: `docker compose up`.
- [ ] 7.3 Verificar que el contenedor arranca y queda escuchando en el puerto 3001: `docker compose logs` debe mostrar el binding en `http://0.0.0.0:3001`.
- [ ] 7.4 Probar el path `/mcp` con un health check básico: `curl -i http://localhost:3001/mcp` debe devolver un 400 o 405 (porque sin body JSON-RPC no es invocable), NO un 404.
- [ ] 7.5 Probar que el path antiguo `/` devuelve 404: `curl -i http://localhost:3001/` debe devolver 404.

## 8. Smoke test end-to-end con Docmost real

- [ ] 8.1 Arrancar una instancia de Docmost en `http://localhost:3000` (puede ser con `docker run -d -p 3000:3000 docmost/docmost` o el setup local que use el proyecto).
- [ ] 8.2 Configurar el `.env` del proyecto MCP con `DOCMOST_URL=http://localhost:3000`, `DOCMOST_EMAIL=...`, `DOCMOST_PASSWORD=...`, `DOCMOST_MCP_TRANSPORT=http`.
- [ ] 8.3 Conectar un cliente MCP (puede ser la extensión MCP de VS Code, o un script que use `mcp-client`) al endpoint `http://localhost:3001/mcp`.
- [ ] 8.4 Invocar el tool `list_spaces`. **Debe funcionar sin error 401** (que era el síntoma del bug original). Validar que devuelve un array JSON con los espacios del workspace.
- [ ] 8.5 Invocar el tool `get_space_info` con un `spaceId` real. Validar respuesta.
- [ ] 8.6 Invocar el tool `search_pages` con un query. Validar respuesta.
- [ ] 8.7 Invocar el tool `create_page` con un título y contenido markdown. Validar que crea la página y devuelve un objeto `Page`.
- [ ] 8.8 Invocar el tool `update_page` sobre la página recién creada. Validar que el contenido se actualiza.
- [ ] 8.9 Invocar el tool `delete_page` sobre la misma página. Validar soft-delete.
- [ ] 8.10 Invocar el tool `list_sidebar_pages` con un `spaceId` real. Validar respuesta.

## 9. Documentación

- [ ] 9.1 En `README.md`, sección "Usage > HTTP mode", cambiar la URL de `http://localhost:3001` a `http://localhost:3001/mcp`.
- [ ] 9.2 En `README.md`, sección "Troubleshooting", añadir fila: `404 al conectar al endpoint HTTP | Apuntando a http://host:port/ en lugar de http://host:port/mcp. Añade /mcp al final de la URL.`
- [ ] 9.3 (Opcional) En `README.md`, sección "How it runs", añadir una nota sobre el path `/mcp` siendo el patrón estándar del SDK.
- [ ] 9.4 Verificar que la spec `openspec/specs/mcp-transport/spec.md` ya contiene el requisito "Path del endpoint HTTP streamable" (lo escribimos como delta del change; tras el archive, OpenSpec lo promoverá al spec principal).
- [ ] 9.5 Verificar que la spec `openspec/specs/native-aot-publishing/spec.md` ya contiene los nuevos requisitos sobre el cliente (ídem).

## 10. Commit y PR

- [ ] 10.1 Stagear todos los archivos modificados (NO `bin/`, `obj/`, `*.user`, `.vs/`).
- [ ] 10.2 Commitear con mensaje convencional: `fix(aot): complete source-gen serialization in client layer + serve HTTP at /mcp`.
- [ ] 10.3 Pushear la rama: `git push -u origin feature/aot-client-serialization`.
- [ ] 10.4 Abrir PR a `develop` con título `fix(aot): complete source-gen serialization in client layer + serve HTTP at /mcp` y descripción enlazando al change OpenSpec.
- [ ] 10.5 Tras el merge con `--no-ff`, ejecutar `openspec archive fix-aot-client-serialization` para promover los deltas a los specs principales.
