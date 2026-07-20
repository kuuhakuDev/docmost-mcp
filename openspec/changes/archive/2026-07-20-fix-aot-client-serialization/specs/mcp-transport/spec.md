# Spec Delta: mcp-transport

## ADDED Requirements

### Requirement: Path del endpoint HTTP streamable

Cuando el servidor se ejecuta en modo HTTP, el transporte streamable MCP DEBE (MUST) servirse en el path `/mcp` (no en `/` ni en cualquier otro path raíz). Esto se logra mediante la llamada `app.MapMcp("/mcp")` en `Program.cs`. El path es absoluto y fijo: no se configura por variable de entorno ni se infiere del hostname.

#### Scenario: Endpoint HTTP en path /mcp

- **WHEN** el servidor arranca en modo HTTP (`DOCMOST_MCP_TRANSPORT=http`)
- **THEN** el endpoint streamable MCP queda registrado bajo `POST /mcp`, `GET /mcp` y `DELETE /mcp`
- **AND** el path raíz `/` queda libre para futuros endpoints (health, métricas, UI)

#### Scenario: URL absoluta del endpoint HTTP

- **WHEN** el servidor escucha en `http://0.0.0.0:3001` (puerto por defecto de `DOCMOST_MCP_PORT`)
- **THEN** un cliente MCP que quiera invocar tools envía requests a `http://<host>:3001/mcp`
- **AND** una request a `http://<host>:3001/` (sin `/mcp`) recibe 404 del routing de ASP.NET Core

#### Scenario: Path no se altera por variables de entorno

- **WHEN** el servidor está configurado con cualquier valor válido de `DOCMOST_URL` y `DOCMOST_MCP_PORT`
- **THEN** el path del endpoint MCP sigue siendo `/mcp` (no se concatena con `DOCMOST_URL` ni con el puerto)

#### Scenario: El path /mcp se documenta en el README

- **WHEN** un desarrollador lee la sección "Usage > HTTP mode" del `README.md`
- **THEN** el ejemplo de URL apunta a `http://localhost:3001/mcp` (con el path explícito)
- **AND** la sección "Troubleshooting" menciona que apuntar a `http://localhost:3001/` (sin `/mcp`) produce 404
