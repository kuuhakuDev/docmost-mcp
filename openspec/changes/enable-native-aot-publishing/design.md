## Context

El proyecto `docmost-mcp` es un servidor MCP para [Docmost](https://docmost.com) implementado en .NET 10 con el SDK oficial de Model Context Protocol (`ModelContextProtocol.AspNetCore 1.2.0`). El proyecto está contenedorizado y desplegado vía `docker-compose`.

**Estado actual problemático:**

1. El `.csproj` declara `PublishAot=true`, `SelfContained=true` y `PublishSingleFile=true` con `OutputType=Exe`. Esto es la **intención** de AOT.
2. El `Dockerfile` (commit `9092002`) reescribió la build a **framework-dependent** mediante `dotnet publish --self-contained false /p:PublishSingleFile=false /p:PublishSelfContained=false`. Los flags de línea de comandos anulan los del csproj, por lo que el binario que se genera **no es AOT**, es una app .NET tradicional multi-archivo.
3. La imagen final es `dhi.io/aspnetcore:10` (imagen no oficial de DHI con el runtime ASP.NET completo) y se arranca con `ENTRYPOINT ["dotnet", "DocMostMcp.Server.dll"]`. Esto es correcto para una app tradicional pero **incompatible con AOT** (AOT produce un binario ELF nativo, no un .dll).
4. `Program.cs` usa `WebApplication.CreateBuilder()` y `.WithToolsFromAssembly()`. Ambos funcionan con JIT, pero violan las restricciones AOT:
   - El SDK MCP marca `WithToolsFromAssembly` con `[RequiresUnreferencedCode]` y avisa explícitamente: *"might not work in Native AOT. Use the generic `WithTools` method instead."*
   - `CreateBuilder` carga providers incompatibles con AOT (HTTPS, HTTP/3, regex constraints en routing).
5. `DocmostTools.cs` serializa envelopes JSON con objetos anónimos (`new { ok = true, statusCode, data, error, details }`). `System.Text.Json` con reflexión **no funciona en AOT**; necesita source generators.
6. El proyecto de tests referencia al Server. Si el Server se vuelve AOT-puro, los mocks con `NSubstitute` sobre tipos del Server pueden generar trim warnings al compilar tests.

**Por qué este cambio es importante ahora:**

- El último commit (`9092002`) intentó resolver un problema de multi-arch con framework-dependent, lo que **regresionó** la intención AOT del csproj. Restaurar la coherencia es una deuda técnica reciente.
- El proyecto está en `0.1.0-beta`; este es el momento de alinear la infraestructura de build antes de que el shape se congele con el primer release "real".
- El SDK MCP C# está evolucionando hacia mejor soporte AOT (los issues abiertos en el repo lo confirman); alinearnos con las prácticas AOT nos prepara para upgrades futuros del SDK.

## Goals / Non-Goals

**Goals:**

- Producir una imagen Docker final basada en `runtime-deps` (sin runtime .NET) que contenga **únicamente el binario nativo** y los archivos mínimos.
- Conseguir que `dotnet publish -c Release` desde un host con `clang` produzca un binario ELF funcional en linux-musl-x64.
- Eliminar todas las advertencias AOT (`IL3050`, `IL2026`, `IL3058`) o documentarlas como suprimidas con justificación.
- Mantener intacta la API pública del servidor MCP: mismo comportamiento, mismos envelopes, mismo protocolo.
- Que el `docker compose up` levante el servicio sin pasos manuales adicionales al usuario.

**Non-Goals:**

- Soportar multi-arquitectura (`linux-x64` y `linux-arm64`) en el mismo Dockerfile. La imagen actual ya publica para `linux-musl-x64` (RID listado en el csproj); multi-arch se puede abordar después con `buildx`.
- Habilitar HTTPS, HTTP/3 o autenticación adicional. Estos features están explícitamente fuera de compatibilidad AOT en este momento.
- Publicar la imagen a un registry. Sólo se construye localmente vía docker-compose.
- Cambiar el protocolo MCP ni los nombres/descripciones de los 8 tools existentes.
- Reescribir `DocmostClient` o `CookieSessionStore`: ambas son AOT-friendly (no usan reflexión, sólo `HttpClient` y `CookieContainer`).

## Decisions

### Decisión 1 — RID: `linux-musl-x64`

- **Por qué**: El `.csproj` ya lista `linux-musl-x64` en `<RuntimeIdentifiers>`. Publicar para musl (Alpine-compatible) da imágenes base más pequeñas (`runtime-deps:10.0-alpine` ~30 MB) que la variante glibc.
- **Por qué no `linux-x64` (glibc)**: más grande y redundante con el csproj actual.
- **Implicación**: la imagen runtime final debe ser Alpine o `chiseled-aot` (basadas en musl).
- **Validación**: `dotnet publish -c Release -r linux-musl-x64` debe terminar sin errores desde el SDK oficial.

### Decisión 2 — Build image: `mcr.microsoft.com/dotnet/sdk:10.0`

- **Por qué**: Es la imagen oficial con las herramientas de .NET. Hay que instalar manualmente `clang` y `zlib1g-dev` (prerrequisitos documentados del compilador AOT en Linux).
- **Por qué no `dhi.io/dotnet:10-sdk`**: Es la imagen actual pero no es oficial. Para AOT conviene la línea oficial de Microsoft porque sus tags siguen cadencias conocidas y los prerrequisitos están bien documentados.
- **Por qué no `mcr.microsoft.com/dotnet/sdk:10.0-aot`**: Existe y trae los prerrequisitos preinstalados, pero **duplica el tamaño** de la imagen base (~500 MB vs ~250 MB) y la build AOT la vamos a hacer una sola vez por imagen Docker. Mejor partir de la SDK oficial y añadir dos paquetes `apt-get`.

### Decisión 3 — Runtime image: `mcr.microsoft.com/dotnet/runtime-deps:10.0-alpine`

- **Por qué**: `runtime-deps` no contiene el runtime .NET (necesario para AOT, ya que el binario es nativo). Variante Alpine (musl) para casar con el RID.
- **Por qué no `dhi.io/aspnetcore:10`**: contiene el runtime .NET completo (innecesario para AOT) y no es oficial.
- **Por qué no `10.0-noble-chiseled-aot`**: el variant `chiseled-aot` es todavía más pequeño y específicamente optimizado para AOT. **Es la mejor opción para producción**, pero por simplicidad inicial usamos `alpine`. Migrar a chiseled es trivial después.

### Decisión 4 — `CreateSlimBuilder` en lugar de `CreateBuilder`

- **Por qué**: Documentado por Microsoft como la variante que omite features incompatibles con AOT (HTTPS, HTTP/3, IIS, regex constraints). Reduce el tamaño del binario AOT y evita trim warnings.
- **Aplicar sólo a HTTP**: el modo `stdio` usa `Host.CreateApplicationBuilder`, que es independiente de ASP.NET Core y no aplica.

### Decisión 5 — `WithTools<DocmostTools>()` en lugar de `WithToolsFromAssembly()`

- **Por qué**: La documentación del SDK MCP marca `WithToolsFromAssembly` con `[RequiresUnreferencedCode]`. La versión genérica `WithTools<T>` se basa en el tipo genérico (resuelto en tiempo de compilación) y es AOT-safe.
- **Trade-off**: si en el futuro se añaden nuevas clases `[McpServerToolType]`, hay que añadirlas explícitamente a la llamada `.WithTools<>()` (no hay scan automático). Aceptable porque el proyecto tiene una sola clase de tools y añadir más será un cambio explícito.

### Decisión 6 — `JsonSerializerContext` con tipos explícitos

- **Por qué**: AOT no soporta reflexión. `System.Text.Json` necesita source generators.
- **Estrategia**: crear `AppJsonSerializerContext` (partial) con `[JsonSerializable(typeof(...))]` por cada tipo que cruce el boundary HTTP/JSON. En este proyecto son los tipos de `DocmostClient.Models` (`Space`, `Page`, `SearchResult`, `SidebarPageItem`, `ApiError`, etc.) más los dos nuevos envelopes (`OkResponse<T>`, `ErrorResponse`).
- **Por qué no `JsonSerializerIsReflectionEnabledByDefault=true` con `JsonTypeInfoResolver.Combine(...)`**: posible pero híbrida; deja superficie de error. Mejor lista cerrada.
- **Por qué no `object` / `Dictionary<string, object>` en el context**: es lo que recomienda evitar el issue #227 del SDK MCP con AOT.

### Decisión 7 — Envelope JSON tipado (no anónimo)

- **Por qué**: AOT no puede serializar tipos anónimos. Tipar el envelope como `OkResponse<T>` y `ErrorResponse` permite registrarlos en el `JsonSerializerContext` y obtener contratos JSON estables.
- **Diseño de tipos** (mismo byte-en-byte que el actual):
  ```csharp
  public sealed record OkResponse<T>([property: JsonPropertyName("ok")] bool Ok, ...);
  public sealed record ErrorResponse([property: JsonPropertyName("ok")] bool Ok, ...);
  ```
- **Validación**: la salida JSON debe ser idéntica a la actual (mismas claves, mismo orden conceptual, mismos tipos: `bool`, `int`, `object`).

### Decisión 8 — Ajuste del proyecto de tests

- **Por qué**: Si el Server se vuelve `<PublishAot>true</PublishAot>`, las referencias al Server desde los tests pueden arrastrar trim warnings incluso aunque los tests no se publiquen AOT.
- **Estrategia**: en `DocMostMcp.Server.csproj` añadir `<IsAotCompatible>true</IsAotCompatible>` y `<IsTrimmable>true</IsTrimmable>` para que el assembly se marque como AOT-compatible. En el `.csproj` de tests, no aplicar AOT (`<PublishAot>false</PublishAot>` por defecto, que es lo que ya hay) y verificar que `NSubstitute` no se vea afectado (NSubstitute usa Castle.DynamicProxy, que **no** es AOT-compatible, pero los tests no se compilan AOT, así que sólo importa que la referencia compile).

## Risks / Trade-offs

- **[Risk] Warnings de AOT en dependencias transitivas (HTTP client, etc.)** → Mitigation: ejecutar el publish localmente, revisar el log, suprimir sólo lo confirmado como no accionable con `#pragma warning disable IL3050` y comentario justificando.
- **[Risk] `WithTools<DocmostTools>()` se olvide al añadir nuevos tool classes** → Mitigation: documentar en el README que añadir un nuevo `[McpServerToolType]` requiere editar `Program.cs`. Considerar extraer una constante `KnownToolTypes` para hacer esto más visible.
- **[Risk] Tamaño del binario AOT se dispara** → Mitigation: empezar con `linux-musl-x64` y Alpine; activar `<InvariantGlobalization>true</InvariantGlobalization>` para eliminar ICU; medir tamaño antes/después. Si es > 70 MB, evaluar `chiseled-aot`.
- **[Risk] Imagen base cambia (Microsoft publica updates)** → Mitigation: pinear la versión mayor (`10.0` sin `-preview`); documentar proceso de bump.
- **[Trade-off] Build más lento**: la compilación AOT es significativamente más lenta que JIT. Aceptable porque se hace una vez por imagen Docker.
- **[Trade-off] Pérdida de flexibilidad de reflection**: si en el futuro se quiere serializar un objeto arbitrario, hay que añadirlo al context. Documentado y aceptado.
- **[Trade-off] `WithTools<T>` no escanea el assembly**: hay que actualizar `Program.cs` al añadir tools. Aceptable dado el tamaño del proyecto.

## Migration Plan

1. Crear la rama `feature/native-aot-docker-publishing` desde `develop`. ✅ (hecho)
2. Aplicar los cambios del `tasks.md` en commits separados por archivo para fácil revisión.
3. Validar localmente:
   - `dotnet publish -c Release -r linux-musl-x64` desde la raíz del Server debe terminar con 0 errores y 0 warnings AOT nuevos.
   - `docker compose build` debe terminar con éxito.
   - `docker compose up` debe arrancar y responder en `localhost:3001` (o el puerto configurado).
4. Verificar el envelope JSON: las respuestas de los 8 tools deben tener la misma estructura que antes (claves `ok`, `statusCode`, `data`, `error`, `details`).
5. Smoke test E2E: configurar `DOCMOST_URL` apuntando a una instancia Docmost real (o mock), ejecutar cada tool y validar la respuesta.
6. Una vez validado, mergear a `develop` con `--no-ff` (Git Flow).
7. Si en el futuro se quiere multi-arch: añadir `--platform linux/amd64,linux/arm64` a `docker buildx build`. Fuera de scope de este cambio.

**Rollback:** revertir la rama antes del merge a `develop`. No hay migraciones de datos, sólo cambios de build/código.

## Open Questions

- **(Resuelto) ¿Incluir el ajuste de tests en este cambio o en uno separado?** → Incluido en este cambio, según preferencia del usuario.
- **(Resuelto) ¿Tipar el envelope o usar `Dictionary<string, object>` en el context?** → Tipado (enfoque A), según preferencia del usuario.
- **(Pendiente) ¿`runtime-deps:10.0-alpine` o `10.0-noble-chiseled-aot`?** → Empezamos con `alpine` por simplicidad; chiseled es candidato para follow-up. Documentar la decisión.
- **(Pendiente) ¿Bajar la versión del SDK MCP si aparecen incompatibilidades AOT insalvables?** → El SDK actual (1.2.0) tiene issues documentados con AOT pero el workaround (`WithTools<T>`) está claro. Si aparecen más, evaluar upgrade o pin a 1.1.x.
