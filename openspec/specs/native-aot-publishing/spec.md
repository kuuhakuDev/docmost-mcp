# Spec: native-aot-publishing

## Purpose

Define los requisitos para compilar y distribuir el servidor MCP como un binario nativo Ahead-of-Time (AOT), eliminando la dependencia del runtime .NET en el contenedor de destino y optimizando el tamaño y tiempo de arranque de la imagen Docker.

## Requirements

### Requirement: Native AOT compilation
El servidor MCP MUST publicarse como binario nativo Ahead-of-Time (AOT) para un RID específico (linux-musl-x64), produciendo un único archivo ELF ejecutable sin dependencia del runtime .NET en el contenedor de destino.

#### Scenario: Publish produces a native ELF binary
- **WHEN** se ejecuta `dotnet publish -c Release -r linux-musl-x64` sobre el proyecto del servidor
- **THEN** el comando termina con código 0
- **AND** el artefacto publicado es un único archivo ELF ejecutable (verificable con `file ...`)

#### Scenario: Binary runs without the .NET runtime installed
- **WHEN** se ejecuta directamente el binario publicado en un sistema sin runtime .NET
- **THEN** el proceso arranca y expone el servicio (stdio o HTTP) sin errores de "runtime not found" o "assembly load failure"

#### Scenario: No AOT warnings on a clean publish
- **WHEN** se ejecuta el publish sobre un árbol de código sin cambios
- **THEN** el log no contiene warnings nuevos del tipo `IL2026`, `IL3050` o `IL3058` que no estuvieran documentados como suprimidos con justificación

### Requirement: Single-image Docker deployment
La imagen Docker final MUST estar basada en `mcr.microsoft.com/dotnet/runtime-deps` (sin runtime .NET), contener únicamente el binario nativo y los archivos mínimos, y arrancar el binario directamente con `ENTRYPOINT` sin invocar el comando `dotnet`.

#### Scenario: Runtime image contains no .NET runtime
- **WHEN** se inspecciona la imagen Docker final
- **THEN** el sistema operativo base es Alpine o equivalente musl
- **AND** no contiene el runtime .NET (`/usr/share/dotnet` no existe o está vacío)
- **AND** el tamaño total de la imagen es menor o igual a 200 MB

#### Scenario: Container starts the native binary directly
- **WHEN** se ejecuta `docker run <imagen>` con las variables de entorno necesarias
- **THEN** el `PID 1` del contenedor es el binario nativo del servidor, no `dotnet`

#### Scenario: docker compose up brings the service online
- **WHEN** se ejecuta `docker compose up` con un `.env` válido
- **THEN** el servicio queda escuchando en el puerto configurado antes de 2 segundos desde el arranque del contenedor

### Requirement: AOT-compatible HTTP transport
El modo de transporte HTTP MUST construirse con `WebApplication.CreateSlimBuilder` y exponer los endpoints de MCP mediante Minimal APIs, evitando las features de ASP.NET Core incompatibles con AOT (HTTPS, HTTP/3, IIS, regex constraints).

#### Scenario: HTTP mode uses CreateSlimBuilder
- **WHEN** se inicia el servidor con `DOCMOST_MCP_TRANSPORT=http`
- **THEN** el `WebApplicationBuilder` subyacente se construye con `WebApplication.CreateSlimBuilder`
- **AND** no se carga el middleware de HTTPS ni de HTTP/3

### Requirement: JSON serialization via source generators
Toda la serialización y deserialización JSON en el servidor MUST usar `System.Text.Json` con source generators, declarados en un `JsonSerializerContext` parcial, sin recurrir a reflexión en runtime.

#### Scenario: No reflection-based JSON at runtime
- **WHEN** se publica el servidor con AOT y se inspeccionan los IL warnings
- **THEN** no aparece ningún warning `IL2026` (Members annotated with 'RequiresUnreferencedCodeAttribute') relativo a `System.Text.Json`
- **AND** la propiedad `JsonSerializerIsReflectionEnabledByDefault` está fijada a `false` en el csproj

#### Scenario: All tool envelopes are typed
- **WHEN** los 8 tools del servidor producen un resultado (éxito o error)
- **THEN** la serialización usa las clases selladas `OkResponse<T>` y `ErrorResponse` registradas en el `JsonSerializerContext`
- **AND** la salida JSON conserva byte-en-byte las claves `ok`, `statusCode`, `data`, `error` y `details` del comportamiento previo

### Requirement: AOT-safe tool registration
El registro de los tools MCP MUST usar la API genérica `WithTools<T>` del SDK de Model Context Protocol, no `WithToolsFromAssembly`, para evitar la reflexión en runtime que el SDK marca explícitamente como incompatible con AOT.

#### Scenario: Tools are registered via the generic API
- **WHEN** se configura el `IMcpServerBuilder` en `Program.cs`
- **THEN** la llamada a `WithTools<DocmostTools>()` está presente
- **AND** NO hay ninguna llamada a `WithToolsFromAssembly()` en el código

#### Scenario: New tool types require explicit registration
- **WHEN** un desarrollador añade una nueva clase con `[McpServerToolType]`
- **THEN** la clase no queda automáticamente expuesta como tool
- **AND** la clase debe ser añadida explícitamente a la lista de `WithTools<>()` en `Program.cs`
- **AND** el README documenta este requisito

### Requirement: Build infrastructure uses official Microsoft images
El `Dockerfile` MUST usar imágenes oficiales de Microsoft para el build (`mcr.microsoft.com/dotnet/sdk`) y el runtime (`mcr.microsoft.com/dotnet/runtime-deps`), pineadas a una versión mayor, sin imágenes de terceros (DHI u otras).

#### Scenario: Build stage uses the official .NET SDK
- **WHEN** se construye la imagen con `docker build`
- **THEN** la primera línea del Dockerfile es `FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build`
- **AND** se instalan los prerrequisitos del compilador AOT (`clang` y `zlib1g-dev`) durante el stage de build

#### Scenario: Runtime stage uses runtime-deps
- **WHEN** se construye la imagen con `docker build`
- **THEN** el stage final usa `FROM mcr.microsoft.com/dotnet/runtime-deps:10.0-alpine AS runtime` o variante equivalente
- **AND** el `ENTRYPOINT` invoca el binario nativo directamente, sin `dotnet X.dll`

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

### Requirement: Explicit JsonPropertyName on positional record parameters

Todo `record` posicional (definido como `record TypeName(Param1, Param2)`) que cruce la frontera de serialización JSON MUST anotar explícitamente cada parámetro con `[property: JsonPropertyName("...")]`. No debe confiarse en `PropertyNamingPolicy` para determinar el nombre JSON de los parámetros de un record posicional, debido a un bug confirmado del source generator ([dotnet/runtime#63542](https://github.com/dotnet/runtime/issues/63542), [dotnet/runtime#113045](https://github.com/dotnet/runtime/issues/113045)) que impide que la política de nomenclatura se aplique consistentemente en todas las plataformas (en particular, ARM64 Native AOT).

#### Scenario: LoginRequest usa JsonPropertyName explícito

- **WHEN** `LoginRequest` se serializa mediante `AppJsonSerializerContext.Default.LoginRequest` en ARM64 Native AOT
- **THEN** el JSON generado contiene `"email"` y `"password"` como nombres de propiedad (no `"Email"` ni `"Password"`)
- **AND** Docmost acepta el login request sin error 401
- **AND** esto funciona aunque `PropertyNamingPolicy` esté configurado a `CamelCase` en el `JsonSourceGenerationOptions`

#### Scenario: Otros records posicionales siguen la misma regla

- **WHEN** se añade un nuevo `record` posicional al `AppJsonSerializerContext`
- **THEN** todos sus parámetros tienen `[property: JsonPropertyName("...")]` explícito
- **AND** no se omite esta anotación alegando que `PropertyNamingPolicy` ya debería cubrirlo
