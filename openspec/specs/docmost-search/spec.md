# Docmost Search

## Purpose

Proporcionar un tool MCP para buscar páginas en Docmost por texto completo, con filtros opcionales por espacio, creador y paginación.

## Requirements

### Requirement: Buscar páginas por texto completo

El servidor DEBE (MUST) exponer un tool `search_pages` que invoca `POST {DOCMOST_URL}/api/search` con `{ query, spaceId?, creatorId?, limit?, offset? }` y devuelve los resultados con su información de espacio.

#### Scenario: Búsqueda global
- **WHEN** un cliente MCP invoca `search_pages` con `query: "roadmap"` y sin `spaceId`
- **THEN** el tool envía `query: "roadmap"` sin `spaceId` y devuelve todos los resultados accesibles para el usuario, con su `id`, `title`, `icon`, `parentPageId`, `creatorId`, `rank`, `highlight`, `createdAt`, `updatedAt` y `space`

#### Scenario: Búsqueda restringida a un espacio
- **WHEN** un cliente MCP invoca `search_pages` con `query` y `spaceId`
- **THEN** el tool envía ambos parámetros y devuelve solo los resultados de ese espacio

#### Scenario: Búsqueda con paginación
- **WHEN** un cliente MCP invoca `search_pages` con `query`, `limit: 10` y `offset: 20`
- **THEN** el tool envía los tres parámetros y devuelve como máximo 10 resultados saltándose los primeros 20

#### Scenario: Búsqueda con filtro por creador
- **WHEN** un cliente MCP invoca `search_pages` con `query` y `creatorId`
- **THEN** el tool devuelve solo las páginas creadas por ese usuario

#### Scenario: Query vacía
- **WHEN** un cliente MCP invoca `search_pages` con `query: ""` o sin `query`
- **THEN** el tool devuelve un error indicando que `query` es obligatorio
