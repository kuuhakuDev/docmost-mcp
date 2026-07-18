# Spec: native-aot-publishing

## ADDED Requirements

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
