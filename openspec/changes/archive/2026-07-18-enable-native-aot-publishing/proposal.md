## Why

El servidor MCP ya solicita publicación AOT en el `.csproj` (`<PublishAot>true</PublishAot>`), pero el `Dockerfile` y el código de `Program.cs` están escritos para una aplicación .NET tradicional (JIT, framework-dependent). El resultado real hoy es una app de ~400 MB basada en imágenes no oficiales, que ignora la configuración AOT del proyecto y tarda en arrancar.

Queremos alinear código, csproj y Dockerfile para producir **una imagen AOT real**: binario nativo ELF, imagen final basada en `runtime-deps` (~100–150 MB), arranque en milisegundos y supply-chain ligado exclusivamente a imágenes oficiales de Microsoft.

## What Changes

- **Habilitar AOT de forma coherente en el `.csproj`**: añadir las propiedades que faltan para forzar análisis AOT en tiempo de compilación y reducir el tamaño del binario (`JsonSerializerIsReflectionEnabledByDefault`, `InvariantGlobalization`).
- **Reescribir el `Dockerfile`** para usar las imágenes oficiales de Microsoft (`mcr.microsoft.com/dotnet/sdk:10.0` para build con `clang` + `zlib1g-dev`, `mcr.microsoft.com/dotnet/runtime-deps:10.0` para runtime), publicando para un RID específico (linux-musl-x64) y arrancando el binario nativo directamente, sin `dotnet X.dll`.
- **Refactorizar `Program.cs` (modo HTTP)**: cambiar `WebApplication.CreateBuilder` por `WebApplication.CreateSlimBuilder` (omite features incompatibles con AOT como HTTPS, HTTP/3 e IIS), cambiar `WithToolsFromAssembly()` por `WithTools<DocmostTools>()` (la API genérica del SDK de MCP es la única AOT-safe, según su propia documentación), y registrar un `JsonSerializerContext` con `ConfigureHttpJsonOptions` para que `System.Text.Json` use source generators en vez de reflexión.
- **Crear un `AppJsonSerializerContext` parcial** que liste todos los tipos que se serializan por la Minimal API: `OkResponse<T>`, `ErrorResponse`, y los tipos de dominio que ya expone el proyecto (`Space`, `Page`, `SearchResult`, `SidebarPageItem`, etc.).
- **Tipar el envelope JSON de los tools** en `DocmostTools.cs`: reemplazar los `new { ok, statusCode, data, error, details }` anónimos por dos clases selladas `OkResponse<T>` y `ErrorResponse`, registradas en el `JsonSerializerContext`. Esto elimina el uso de reflexión y hace que los tools sean seguros bajo AOT.
- **Ajustar el proyecto de tests** (`DocMostMcp.Server.Tests.csproj`) para que no introduzca trimming/AOT warnings al referenciar al servidor; verificar que `NSubstitute` sigue siendo compatible con el modo AOT del servidor (los tests no se publican AOT, pero la referencia debe compilar limpiamente).

## Capabilities

### New Capabilities

- `native-aot-publishing`: requisitos de infraestructura de build y deployment que
  garantizan que el servidor se publica como binario nativo AOT, con imagen
  Docker basada en `runtime-deps` y arranque medido en milisegundos. Esta
  capacidad documenta el contrato de "cómo se construye" el servidor, no su
  API funcional. La API MCP pública (los 8 tools, sus nombres, sus esquemas y
  el envelope JSON) no cambia.

### Modified Capabilities

<!-- No aplica: ningún requisito observable cambia. Los 8 tools siguen
     exponiendo el mismo esquema, los mismos nombres y el mismo envelope JSON.
     Sólo cambia cómo se compila y se empaqueta el ejecutable. -->

## Impact

**Archivos modificados:**

- `DocMostMcp.Server/DocMostMcp.Server.csproj` — propiedades AOT/trimming.
- `DocMostMcp.Server/Program.cs` — `CreateSlimBuilder`, `WithTools<T>`, registro de `JsonSerializerContext`.
- `DocMostMcp.Server/Tools/DocmostTools.cs` — refactor del envelope a clases tipadas.
- `DocMostMcp.Server/Tools/OkResponse.cs` *(nuevo)* — `OkResponse<T>` sellada.
- `DocMostMcp.Server/Tools/ErrorResponse.cs` *(nuevo)* — `ErrorResponse` sellada.
- `DocMostMcp.Server/Json/AppJsonSerializerContext.cs` *(nuevo)* — contexto STJ con source generator.
- `Dockerfile` — reescritura completa.
- `DocMostMcp.Server.Tests/DocMostMcp.Server.Tests.csproj` — ajustes para evitar warnings AOT/trim.
- `docker-compose.yml` — sin cambios funcionales; documentar la nueva variable `DOCMOST_MCP_TRANSPORT` ya presente.
- `README.md` — actualizar la sección de "How it runs" con la mención a la imagen AOT.

**Sin cambios en:**

- API pública MCP (los 8 tools).
- Variables de entorno (`DOCMOST_URL`, `DOCMOST_EMAIL`, `DOCMOST_PASSWORD`, `DOCMOST_MCP_TRANSPORT`, `DOCMOST_MCP_PORT`).
- Comportamiento en runtime: mismos envelopes, mismos códigos de estado, misma semántica de reintentos de auth.
- Protocolo de transporte (`stdio` / HTTP stateless).

**Riesgo de regresión:** medio-bajo. La estructura de los envelopes JSON se conserva byte a byte (mismas claves `ok`, `statusCode`, `data`, `error`, `details`), pero el tipo cambia de objeto anónimo a clase sellada: cualquier consumidor que se apoye en reflection sobre el `JsonElement` verá los mismos datos, pero un consumidor que se apoye en el tipo CLR (poco probable, ya que el envelope se serializa antes de salir) tendría que actualizar.
