# Spec Delta: native-aot-publishing

## ADDED Requirements

### Requirement: AOT-safe JSON serialization in the Docmost HTTP client

Todas las llamadas HTTP que el servidor MCP realiza hacia la API de Docmost (en `DocmostClient` y `CookieSessionStore`) MUST usar los overloads AOT-safe de `HttpClientJsonExtensions` y `HttpContentJsonExtensions` que aceptan `JsonTypeInfo<T>` o `JsonSerializerContext`. En particular, no se permite el uso de `PostAsJsonAsync(endpoint, value, JsonSerializerOptions, ct)` ni de `ReadFromJsonAsync<T>(content, JsonSerializerOptions, ct)` cuando el `JsonSerializerOptions` no tiene `TypeInfoResolver` configurado.

#### Scenario: DocmostClient serializa bodies con JsonTypeInfo

- **WHEN** `DocmostClient.PostAsync<T>` envía un body a Docmost (por ejemplo, `POST /api/spaces`, `POST /api/pages/info`, etc.)
- **THEN** la llamada usa el overload `PostAsJsonAsync<TValue>(this HttpClient, string?, TValue, JsonTypeInfo<TValue>, CancellationToken)`
- **AND** el `JsonTypeInfo<TValue>` se obtiene del `AppJsonSerializerContext.Default`
- **AND** el binario AOT no invoca la API de reflexión de `System.Text.Json`

#### Scenario: DocmostClient deserializa respuestas con JsonTypeInfo

- **WHEN** `DocmostClient.PostAsync<T>` lee el body de la respuesta de Docmost
- **THEN** la deserialización usa `JsonSerializer.Deserialize(json, JsonTypeInfo<T>)` o el overload que acepta `JsonSerializerContext`
- **AND** no se llama a `JsonSerializer.Deserialize<T>(json, JsonSerializerOptions)` con opciones sin `TypeInfoResolver`

#### Scenario: CookieSessionStore serializa LoginRequest con JsonTypeInfo

- **WHEN** `CookieSessionStore.LoginAsync` envía `POST /api/auth/login`
- **THEN** la llamada usa el overload AOT-safe de `PostAsJsonAsync` con `AppJsonSerializerContext.Default.LoginRequest`
- **AND** el `LoginRequest` está registrado como `[JsonSerializable(typeof(LoginRequest))]` en el `AppJsonSerializerContext`

#### Scenario: CookieSessionStore deserializa errores con JsonTypeInfo

- **WHEN** `CookieSessionStore.LoginAsync` lee un body de error (status >= 400)
- **THEN** la deserialización usa el overload AOT-safe de `ReadFromJsonAsync<T>(content, JsonTypeInfo<T>, CancellationToken)` con `AppJsonSerializerContext.Default.ApiError`
- **AND** el `ApiError` está registrado en el `AppJsonSerializerContext`

### Requirement: Complete JsonSerializerContext coverage

El `AppJsonSerializerContext` MUST registrar como `[JsonSerializable]` todos los tipos raíz que cruzan la frontera de serialización JSON en el servidor, incluyendo: (a) los envelopes (`OkResponse<T>`, `ErrorResponse`), (b) los modelos de respuesta de Docmost (`Space`, `Page`, `SearchResult`, `SidebarPageItem`, `SpacesListResponse`, `SidebarPagesResponse`, `SearchResponse`, `PaginationMeta`, `ApiError`, `ApiResponse<T>` y sus tipos asociados), (c) los tipos primitivos y de framework que se serializan como raíces o miembros polimórficos (`string[]`, `object`, `Dictionary<string, object>`, `JsonElement`), y (d) los tipos de body de los requests a Docmost (`LoginRequest`).

#### Scenario: Tipos polimórficos object están registrados

- **WHEN** un miembro se declara como `object?` (por ejemplo `ErrorResponse.Details` o `ApiError.Message`)
- **THEN** el tipo `object` aparece como `[JsonSerializable(typeof(object))]` en el `AppJsonSerializerContext`
- **AND** la serialización funciona en el binario AOT sin reflexión

#### Scenario: Bodies como Dictionary están registrados

- **WHEN** los métodos de `DocmostClient` construyen un body como `Dictionary<string, object?>`
- **THEN** `Dictionary<string, object>` aparece como `[JsonSerializable(typeof(Dictionary<string, object>))]` en el `AppJsonSerializerContext`

#### Scenario: Arrays de strings en mensajes de error

- **WHEN** Docmost devuelve un error con `message: ["error1", "error2"]` (array de strings)
- **THEN** `ApiError.FromJsonElement` deserializa ese array usando `AppJsonSerializerContext.Default.StringArray`
- **AND** el tipo `string[]` aparece como `[JsonSerializable(typeof(string[]))]` en el `AppJsonSerializerContext`

### Requirement: MCP SDK resolver encadenado con AppJsonSerializerContext

Cuando se usa un `JsonSerializerContext` custom (`AppJsonSerializerContext`) junto con el transporte HTTP del SDK de MCP, el `JsonSerializerOptions.TypeInfoResolverChain` del `ConfigureHttpJsonOptions` MUST incluir **primero** el resolver del SDK (`McpJsonUtilities.DefaultOptions.TypeInfoResolver!`) y **después** el `AppJsonSerializerContext.Default`. Esto garantiza que los tipos del protocolo MCP (`JsonRpcRequest`, `JsonRpcResponse`, `CallToolRequest`, `ListToolsRequest`, etc.) tengan contrato de serialización cuando el SDK los serializa dentro del transporte HTTP streamable.

#### Scenario: TypeInfoResolverChain contiene ambos resolvers

- **WHEN** se arranca el servidor en modo HTTP
- **THEN** `options.SerializerOptions.TypeInfoResolverChain` contiene al menos dos entradas en este orden: (1) `McpJsonUtilities.DefaultOptions.TypeInfoResolver!`, (2) `AppJsonSerializerContext.Default`
- **AND** la posición 0 es el resolver del SDK de MCP (consultar primero)

#### Scenario: Tipos del protocolo MCP se serializan correctamente

- **WHEN** el SDK de MCP serializa un mensaje del protocolo (por ejemplo, una respuesta a `tools/call`) en el transporte HTTP streamable
- **THEN** la serialización usa el `JsonTypeInfo` provisto por el resolver del SDK
- **AND** el binario AOT no invoca reflexión para serializar tipos del protocolo

## MODIFIED Requirements

<!-- No hay requisitos modificados. Las correcciones se añaden como requisitos nuevos
     (ADDED) para preservar la historia del spec original. -->
