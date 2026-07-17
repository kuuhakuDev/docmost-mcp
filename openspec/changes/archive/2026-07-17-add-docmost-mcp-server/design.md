## Context

El proyecto `DocMostMcp.Server` es un esqueleto generado a partir del template `Microsoft.McpServer.ProjectTemplates` (preview, .NET 10). Hoy contiene:

- `Program.cs` que arranca un `Host` genérico con `WithStdioServerTransport()` y registra `RandomNumberTools` (un placeholder).
- Un `.csproj` con `OutputType=Exe`, `TargetFramework=net10.0`, empaquetado como `McpServer` self-contained single-file.
- `README.md` y `.mcp/server.json` con placeholders del template.
- Documentación de la API de Docmost en `docs/api-docmost/` (formato OpenAPI 3.1 fragmentado por paths).

**Restricciones de la API de Docmost (verificadas en `docs/api-docmost/`):**
- Todos los endpoints son `POST` y devuelven `{ data, success, status }` (excepción: health, descargas).
- La OpenAPI describe `bearerAuth` (API key), pero la edición community expone `/api/auth/login` que acepta `{ email, password }` y devuelve una cookie de sesión en `Set-Cookie`. Esa cookie debe acompañar el resto de peticiones o el servidor responde `401`.
- Paginación cursor-based en endpoints de listado.

**Stack técnico fijado:**
- .NET 10 LTS (soportado hasta noviembre 2028 según Microsoft Learn).
- `ModelContextProtocol` 1.2.0 + `ModelContextProtocol.AspNetCore` 1.2.0 (oficial C# SDK).
- Tests con xUnit, NSubstitute y AwesomeAssertions.

## Goals / Non-Goals

**Goals:**
- Servidor MCP único con dos modos seleccionables en tiempo de arranque (stdio o HTTP streamable).
- Autenticación **transparente** con cookie: el LLM no ve credenciales ni sesiones, nunca invoca un tool de auth.
- Re-login automático y serializado cuando la cookie caduque o sea rechazada (`401`).
- Ocho tools MCP que cubren spaces, navegación, búsqueda y CRUD de páginas en markdown.
- Configuración 100% por variables de entorno (sin argumentos CLI).
- Tests unitarios sin red (con `HttpMessageHandler` mockeado).
- Empaquetado self-contained single-file y publicable en NuGet como `McpServer`.

**Non-Goals:**
- Multi-tenant: una sola credencial configura una sola sesión por proceso. No se acepta auth por request.
- Modificación o creación de espacios, members, comments, attachments, history, trash, export/import, AI, SSO, MFA, API keys: todos los tags EE de la API y los que el usuario no pidió.
- Persistencia de la cookie en disco (siempre en memoria; al reiniciar se re-loguea).
- Rate limiting, métricas o tracing avanzado.
- Tests de integración contra una instancia real de Docmost.

## Decisions

### D1. Un binario, dos bootstrap según el transporte

El SDK oficial expone `WithStdioServerTransport()` (vía `Host.CreateApplicationBuilder`) y `WithHttpTransport()` (vía `WebApplication.CreateBuilder` + `app.MapMcp()`). Son entry points distintos que no se pueden mezclar en un solo `IHost`. La forma idiomática es un `Program.cs` con detección temprana y dos ramas:

```
                       ┌──────────────────────────────┐
                       │  Program.cs (top-level)      │
                       │  Lee env vars                │
                       │  Valida opciones             │
                       └────────────┬─────────────────┘
                                    │
                  DOCMOST_MCP_TRANSPORT
                          ╱           ╲
                       stdio          http
                        │              │
                        ▼              ▼
              ┌────────────────┐  ┌──────────────────────┐
              │ Host.Create... │  │ WebApplication.     │
              │ .AddMcpServer()│  │   CreateBuilder()   │
              │ .WithStdio*()  │  │ .AddMcpServer()     │
              │ .WithTools*()  │  │ .WithHttpTransport()│
              │ .Build().Run() │  │ .WithToolsFromA()*  │
              └────────────────┘  │ app.MapMcp()        │
                                  │ app.RunAsync()      │
                                  └──────────────────────┘
```

- Cliente HTTP de Docmost y `CookieSessionStore` se construyen una sola vez (en cada rama) y se inyectan al registrar tools vía `WithTools<DocmostTools>()` o `WithToolsFromAssembly()`.
- `WithToolsFromAssembly()` descubrirá `DocmostTools` automáticamente (atributo `[McpServerToolType]`) — más robusto si en el futuro se añaden más clases de tools.

**Alternativas consideradas:**
- *Dos binarios en la solución*: más limpio teóricamente, pero duplica bootstrap y obliga a publicar dos paquetes. Descartado por simplicidad.
- *Proceso único con flag en runtime que cambia el transporte*: el SDK no lo soporta (los `IHostedService` de transporte se registran al `Build()`).

### D2. HttpClient directo, NO IHttpClientFactory

La documentación oficial de Microsoft Learn (`HttpClient guidelines`) **advierte explícitamente**: *"If your app requires cookies, it's recommended to avoid using IHttpClientFactory"*. El motivo es que la factoría agrupa `HttpMessageHandler`, y agrupar `CookieContainer` provoca fugas de cookies entre clientes no relacionados. Como nuestra autenticación es justamente por cookies, usamos `HttpClient` de larga vida creado manualmente con un `SocketsHttpHandler` que tiene un `CookieContainer` propio y `UseCookies = true`.

### D3. Autenticación transparente con `DelegatingHandler` + `SemaphoreSlim(1,1)`

```
   ┌──────────────────────────────┐    ┌────────────────────────────┐
   │  CookieSessionStore          │    │  DocmostAuthHandler       │
   │  (singleton)                 │    │  (DelegatingHandler)       │
   │                              │    │                            │
   │  • CookieContainer           │◀──▶│  • Inyecta la cookie      │
   │  • SemaphoreSlim(1,1)        │    │    en cada request        │
   │  • estado de login:          │    │  • Captura 401            │
   │    - isLoggedIn              │    │  • Pide re-login al store │
   │    - lastLoginAt             │    │  • Re-envía la request    │
   │  • DocmostAuth               │    │    UNA vez                │
   │    (LoginAsync)              │    │  • Si vuelve 401, propaga │
   └──────────────────────────────┘    └────────────────────────────┘
```

- **`CookieSessionStore`** es el único punto que llama a `POST /api/auth/login`. Usa `SemaphoreSlim(1,1)` para que N peticiones que detecten `401` en paralelo solo provoquen un login. Tras un login fallido, marca el store como "no autenticable" y devuelve un error claro (sin reintentos).
- **`DocmostAuthHandler`** es un `DelegatingHandler` que se inserta en la pipeline del `HttpClient`. Antes de mandar cada request, espera a que `CookieSessionStore` esté autenticado (con un timeout corto). Si recibe `401`, llama a `EnsureFreshSessionAsync()`, espera a que termine, re-envía la request **una sola vez**. Si vuelve a `401`, lanza `DocmostAuthException` con mensaje sobre credenciales inválidas.
- **Por qué `DelegatingHandler` y no lógica en cada tool**: el handler se aplica a TODA la pipeline. Los tools se mantienen puros (reciben parámetros, devuelven resultados), sin código repetido de auth/retry.

### D4. Formato de respuesta: JSON estructurado con `ok` flag

Cada tool devuelve `JsonNode` (o `JsonElement`) con la forma:

```json
// Éxito
{ "ok": true,  "statusCode": 200, "data": { ... } }

// Error de Docmost
{ "ok": false, "statusCode": 404, "error": "Page not found", "details": { ... } }
```

Para errores recuperables (Docmost 4xx/5xx), se devuelve el JSON anterior sin lanzar excepción — el LLM puede leer el error. Para errores irrecuperables (red caída, credenciales inválidas) se lanza `McpException` para que el protocolo MCP lo marque como fallo de tool.

**Alternativas consideradas:**
- *Devolver `string` markdown*: más legible para humanos pero el LLM pierde estructura y debe parsearlo.
- *Lanzar excepción en cualquier error*: el LLM recibe mensajes truncados por el protocolo.

### D5. Contenido de páginas: solo markdown

El `format` aceptado por las tools `create_page` y `update_page` es siempre `markdown`. Internamente se envía como `format: "markdown"` en el body. La API de Docmost convierte el markdown a ProseMirror en el servidor. El tool `get_page` también pide `format: "markdown"` cuando `includeContent=true`.

**Razón:** el usuario lo pidió así. Simplifica el modelo y elimina ambigüedad de conversión.

### D6. Validación de configuración con `IValidateOptions<T>`

`DocmostOptions` se valida al construir el `Host`/`WebApplication` mediante `Options pattern` + `IValidateOptions<DocmostOptions>`. Si falta `DOCMOST_URL`, `DOCMOST_EMAIL` o `DOCMOST_PASSWORD`, o si `DOCMOST_MCP_TRANSPORT` no es `stdio|http`, la aplicación falla al arrancar con un mensaje claro (no espera al primer tool call).

### D7. Detección de TTY para el default del transporte

Si `DOCMOST_MCP_TRANSPORT` no está definida:
- Si `Console.IsInputRedirected == false` (es decir, hay un TTY) → `stdio` (modo interactivo, dev local).
- Si no → `http` (probablemente un contenedor o un servicio).

La variable explícita siempre gana.

## Risks / Trade-offs

- **Cookie expuesta en logs si el nivel es Trace** → mitigation: el logger de `HttpClient` se filtra al nivel `Warning`; el handler no loguea el cuerpo de las requests (solo método, URL, status).
- **Una sola credencial por proceso** → en HTTP, todos los clientes MCP comparten la misma sesión de Docmost. Si dos clientes hacen requests concurrentes pueden pisarse (cambios en una página afectan a todos). Esto está documentado en el README y es coherente con el "non-goal" de multi-tenant.
- **Re-login concurrente serializado** → si Docmost está muy lento en responder al login, todas las requests en vuelo esperan. Mitigation: timeout de 10s en `EnsureFreshSessionAsync()` y mensaje claro si expira.
- **Tests sin red** → no cubren el contrato real con Docmost (cambios en la API pasan desapercibidos). Mitigation: dejar un test de integración manual documentado en el README para correrlo puntualmente.
- **Dependencia de `ModelContextProtocol.AspNetCore` 1.2.0** → al ser preview, futuras versiones pueden romper. Mitigation: pin de versión exacta y tests de smoke en CI.
- **Cambio de SDK del csproj** → pasar de `Microsoft.NET.Sdk` a `Microsoft.NET.Sdk.Web` cambia el comportamiento de `dotnet build` (incluye todo el árbol `wwwroot` que no aplica aquí). Mitigation: no tenemos `wwwroot`, no hay impacto; queda documentado en tasks.

## Open Questions

Ninguna para el MVP. Items a considerar en iteraciones futuras (no se incluyen en este change):
- Persistencia de la cookie en disco (sqlite/keyring) para no reloguear en cada reinicio.
- Multi-tenant con auth por `Authorization: Bearer` en cada request MCP.
- Tool de `auth_status` visible (descartado por el usuario explícitamente en esta iteración).
- Soporte para adjuntos, comentarios, history, export.
