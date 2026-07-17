## ADDED Requirements

### Requirement: Listar páginas del sidebar

El servidor DEBE (MUST) exponer un tool `list_sidebar_pages` que invoca `POST {DOCMOST_URL}/api/pages/sidebar-pages` con `{ spaceId, pageId?, limit?, cursor?, query? }` y devuelve la respuesta paginada de Docmost.

#### Scenario: Listar páginas raíz de un espacio
- **WHEN** un cliente MCP invoca `list_sidebar_pages` con `spaceId` y sin `pageId`
- **THEN** el tool devuelve las páginas de nivel raíz del espacio

#### Scenario: Listar hijas de una página
- **WHEN** un cliente MCP invoca `list_sidebar_pages` con `pageId`
- **THEN** el tool devuelve las páginas hijas de la página indicada

#### Scenario: Falta spaceId y pageId
- **WHEN** un cliente MCP invoca `list_sidebar_pages` sin `spaceId` ni `pageId`
- **THEN** el tool devuelve un error indicando que al menos uno es obligatorio

### Requirement: Obtener detalle de una página

El servidor DEBE (MUST) exponer un tool `get_page` que invoca `POST {DOCMOST_URL}/api/pages/info` con `{ pageId, includeContent?, format? }` y devuelve la respuesta de Docmost.

#### Scenario: Obtener metadatos sin contenido
- **WHEN** un cliente MCP invoca `get_page` con un `pageId` válido y `includeContent=false`
- **THEN** el tool llama a `POST /api/pages/info` con `includeContent: false` y devuelve la página sin el campo `content`

#### Scenario: Obtener página en markdown
- **WHEN** un cliente MCP invoca `get_page` con un `pageId` válido e `includeContent=true`
- **THEN** el tool envía `includeContent: true, format: "markdown"` en el body y devuelve el contenido en formato markdown

#### Scenario: Página inexistente
- **WHEN** un cliente MCP invoca `get_page` con un `pageId` que no existe
- **THEN** el tool devuelve un objeto JSON con `ok: false, statusCode: 404, error: "Page not found"`

### Requirement: Crear una página en markdown

El servidor DEBE (MUST) exponer un tool `create_page` que invoca `POST {DOCMOST_URL}/api/pages/create` con `{ spaceId, title?, icon?, parentPageId?, content, format: "markdown" }` y devuelve la página creada.

#### Scenario: Crear página raíz con título y contenido markdown
- **WHEN** un cliente MCP invoca `create_page` con `spaceId`, `title` y `content` en markdown
- **THEN** el tool envía `format: "markdown"` siempre, y devuelve la página recién creada con su `id`, `slugId`, `title`, `content` y metadatos

#### Scenario: Crear sub-página
- **WHEN** un cliente MCP invoca `create_page` con `spaceId`, `parentPageId` y `content`
- **THEN** el tool crea la página como hija de la página indicada y devuelve el resultado

#### Scenario: Sin contenido ni título
- **WHEN** un cliente MCP invoca `create_page` con solo `spaceId`
- **THEN** el tool crea una página vacía (título y contenido vacíos)

#### Scenario: spaceId faltante
- **WHEN** un cliente MCP invoca `create_page` sin `spaceId`
- **THEN** el tool devuelve un error indicando que `spaceId` es obligatorio

### Requirement: Actualizar una página en markdown

El servidor DEBE (MUST) exponer un tool `update_page` que invoca `POST {DOCMOST_URL}/api/pages/update` con `{ pageId, title?, icon?, content?, format: "markdown", operation? }` y devuelve la página actualizada.

#### Scenario: Reemplazar contenido
- **WHEN** un cliente MCP invoca `update_page` con `pageId`, `content` y `operation: "replace"`
- **THEN** el tool reemplaza el contenido de la página y la devuelve

#### Scenario: Anexar contenido
- **WHEN** un cliente MCP invoca `update_page` con `pageId`, `content` y `operation: "append"`
- **THEN** el tool añade el contenido al final del documento existente y devuelve la página

#### Scenario: Operación por defecto
- **WHEN** un cliente MCP invoca `update_page` con `pageId` y `content` sin especificar `operation`
- **THEN** el tool usa `operation: "replace"` por defecto

#### Scenario: Solo cambiar el título
- **WHEN** un cliente MCP invoca `update_page` con `pageId` y `title` (sin `content`)
- **THEN** el tool actualiza solo el título y devuelve la página con el nuevo título

#### Scenario: pageId faltante
- **WHEN** un cliente MCP invoca `update_page` sin `pageId`
- **THEN** el tool devuelve un error indicando que `pageId` es obligatorio

### Requirement: Eliminar una página

El servidor DEBE (MUST) exponer un tool `delete_page` que invoca `POST {DOCMOST_URL}/api/pages/delete` con `{ pageId, permanentlyDelete? }` y devuelve el resultado.

#### Scenario: Eliminación lógica (por defecto)
- **WHEN** un cliente MCP invoca `delete_page` con un `pageId` válido sin `permanentlyDelete`
- **THEN** el tool marca la página como eliminada (`deletedAt` poblado) y la devuelve como restaurable desde la papelera

#### Scenario: Eliminación permanente
- **WHEN** un cliente MCP invoca `delete_page` con `pageId` y `permanentlyDelete: true`
- **THEN** el tool elimina la página y todos sus descendientes de la base de datos

#### Scenario: Página inexistente
- **WHEN** un cliente MCP invoca `delete_page` con un `pageId` que no existe
- **THEN** el tool devuelve un objeto JSON con `ok: false, statusCode: 404, error: "Page not found"`
