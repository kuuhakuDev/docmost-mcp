## Why

Hoy la única forma de interactuar programáticamente con Docmost es mediante su API HTTP basada en POST + cookies de sesión. Los asistentes IA (Claude, Copilot, etc.) no pueden consumirla directamente porque no hablan el protocolo HTTP con cookies ni manejan la caducidad de la sesión. Esto obliga a los usuarios a copiar y pegar manualmente entre el chat y el navegador, rompiendo el flujo de trabajo agente.

Este cambio añade un servidor **Model Context Protocol (MCP)** para Docmost que expone los recursos de la API como tools MCP, con **autenticación transparente basada en cookies**: el LLM nunca ve credenciales ni sesiones, simplemente invoca tools. El servidor se puede ejecutar como proceso local (stdio) para IDEs como VS Code/Copilot, o como servicio HTTP streamable para escenarios remotos o multi-cliente.

## What Changes

- **Servidor MCP unificado** (`DocMostMcp.Server`) con arranque configurable vía variable de entorno `DOCMOST_MCP_TRANSPORT` (`stdio` o `http`).
- **Cliente HTTP de Docmost con re-login transparente**: mantiene una `CookieContainer` en memoria, serializa los logins con `SemaphoreSlim(1,1)`, y reintenta una vez ante `401` antes de propagar el error al tool.
- **Ocho tools MCP** que cubren el ciclo de vida de páginas en Docmost:
  - `list_spaces`, `get_space_info` (espacios)
  - `list_sidebar_pages`, `get_page` (lectura)
  - `search_pages` (búsqueda full-text)
  - `create_page`, `update_page`, `delete_page` (CRUD, solo markdown)
- **Tests unitarios** del cliente de autenticación, de los tools (con `HttpMessageHandler` mockeado) y de la configuración.
- **Documentación de uso** en `README.md` con ejemplos de configuración para ambos transportes.
- **Empaquetado NuGet** con `PackageType=McpServer` y binarios self-contained para los RIDs declarados.

## Capabilities

### New Capabilities
- `mcp-transport`: bootstrap de la aplicación, selección de transporte (stdio vs HTTP streamable) y configuración por variables de entorno.
- `docmost-auth`: cliente HTTP con autenticación por cookie de sesión, re-login automático ante 401 y serialización de logins concurrentes.
- `docmost-spaces`: tools para listar y consultar espacios de trabajo del usuario autenticado.
- `docmost-pages`: tools CRUD de páginas (obtener, crear, actualizar, eliminar) y navegación por sidebar, restringido a contenido en markdown.
- `docmost-search`: tool de búsqueda full-text sobre páginas con filtros opcionales por espacio y creador.

### Modified Capabilities
Ninguna. Es la primera iteración del proyecto.

## Impact

- **Código nuevo**:
  - `DocMostMcp.Server/Program.cs` (reescrito)
  - `DocMostMcp.Server/Configuration/DocmostOptions.cs`
  - `DocMostMcp.Server/Client/CookieSessionStore.cs`
  - `DocMostMcp.Server/Client/DocmostAuthHandler.cs`
  - `DocMostMcp.Server/Client/DocmostClient.cs`
  - `DocMostMcp.Server/Client/Models/*.cs` (DTOs)
  - `DocMostMcp.Server/Tools/DocmostTools.cs`
  - `DocMostMcp.Server.Tests/` (nuevo proyecto xUnit)
- **Código eliminado**:
  - `DocMostMcp.Server/Tools/RandomNumberTools.cs` (placeholder del template)
- **Archivos modificados**:
  - `DocmostMcp.slnx` (añadir el proyecto de tests)
  - `DocMostMcp.Server/DocMostMcp.Server.csproj` (cambiar SDK a `Microsoft.NET.Sdk.Web`, añadir `ModelContextProtocol.AspNetCore`, propiedades de paquete actualizadas)
  - `DocMostMcp.Server/.mcp/server.json` (metadata real del paquete)
  - `DocMostMcp.Server/README.md` (instrucciones de uso)
- **Dependencias nuevas**:
  - `ModelContextProtocol.AspNetCore` 1.2.0
  - (solo tests) `xunit`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk`, `NSubstitute`, `AwesomeAssertions`
- **Sistemas externos**:
  - Requiere una instancia de Docmost accesible vía HTTP y credenciales de un usuario válido de la edición community.
  - No requiere cambios en el servidor de Docmost.
