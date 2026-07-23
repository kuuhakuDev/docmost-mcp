# Docmost Spaces

## Purpose

Proporcionar tools MCP para listar espacios y obtener información detallada de un espacio en Docmost.

## Requirements

### Requirement: Listar espacios del usuario

El servidor DEBE (MUST) exponer un tool `list_spaces` que invoca `POST {DOCMOST_URL}/api/spaces` con el body del usuario y devuelve la respuesta envuelta en el formato estándar del servidor.

#### Scenario: Listado exitoso
- **WHEN** un cliente MCP invoca `list_spaces` con paginación por defecto
- **THEN** el tool llama a `POST /api/spaces`, recibe 200 con `data.items` y `data.meta`, y devuelve un objeto JSON con `ok: true, statusCode: 200, data: { items: [...], meta: { limit, hasNextPage, hasPrevPage, nextCursor, prevCursor } }`

#### Scenario: Listado con filtro de texto
- **WHEN** un cliente MCP invoca `list_spaces` con el parámetro `query` igual a "ingeniería"
- **THEN** el tool envía `query: "ingeniería"` en el body y devuelve los espacios filtrados por Docmost

#### Scenario: Listado paginado siguiente
- **WHEN** un cliente MCP invoca `list_spaces` con `cursor` igual al `nextCursor` de la respuesta anterior
- **THEN** el tool envía ese cursor en el body y devuelve la página siguiente de espacios

#### Scenario: Sin acceso a Docmost
- **WHEN** un cliente MCP invoca `list_spaces` y la cookie no es válida
- **THEN** el cliente re-loguea automáticamente y vuelve a invocar el endpoint; si el re-login falla, devuelve un error claro

### Requirement: Obtener detalle de un espacio

El servidor DEBE (MUST) exponer un tool `get_space_info` que invoca `POST {DOCMOST_URL}/api/spaces/info` con `{ spaceId }` y devuelve la respuesta de Docmost.

#### Scenario: Espacio existente
- **WHEN** un cliente MCP invoca `get_space_info` con un `spaceId` válido
- **THEN** el tool devuelve un objeto JSON con `ok: true, statusCode: 200, data: { id, name, slug, description, logo, defaultRole, visibility, settings, creatorId, workspaceId, createdAt, updatedAt, memberCount, membership }`

#### Scenario: Espacio inexistente
- **WHEN** un cliente MCP invoca `get_space_info` con un `spaceId` que no existe
- **THEN** el tool devuelve un objeto JSON con `ok: false, statusCode: 404, error: "Space not found"`

#### Scenario: spaceId faltante
- **WHEN** un cliente MCP invoca `get_space_info` sin `spaceId`
- **THEN** el tool devuelve un error indicando que `spaceId` es obligatorio
