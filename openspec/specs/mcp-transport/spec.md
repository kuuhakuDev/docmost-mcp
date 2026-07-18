# MCP Transport

## Purpose

Gestionar el modo de transporte del servidor MCP (stdio o HTTP streamable) seleccionable por variable de entorno o detección automática, incluyendo configuración de puerto HTTP y validación de credenciales al arranque.

## Requirements

### Requirement: Selección de transporte por variable de entorno

El servidor DEBE (MUST) elegir el modo de transporte (`stdio` o `http`) según la variable de entorno `DOCMOST_MCP_TRANSPORT`. Si la variable no está definida, DEBE (MUST) detectar automáticamente: si la entrada estándar no está redirigida (TTY presente) usar `stdio`, en caso contrario `http`. La variable explícita siempre tiene prioridad sobre la detección automática.

#### Scenario: Transporte explícito stdio
- **WHEN** la variable `DOCMOST_MCP_TRANSPORT=stdio` está definida
- **THEN** el servidor arranca usando el transporte stdio del SDK MCP

#### Scenario: Transporte explícito http
- **WHEN** la variable `DOCMOST_MCP_TRANSPORT=http` está definida
- **THEN** el servidor arranca usando el transporte HTTP streamable del SDK MCP en el puerto indicado por `DOCMOST_MCP_PORT`

#### Scenario: Transporte por defecto con TTY
- **WHEN** la variable `DOCMOST_MCP_TRANSPORT` no está definida y `Console.IsInputRedirected` es `false`
- **THEN** el servidor arranca usando el transporte stdio

#### Scenario: Transporte por defecto sin TTY
- **WHEN** la variable `DOCMOST_MCP_TRANSPORT` no está definida y `Console.IsInputRedirected` es `true`
- **THEN** el servidor arranca usando el transporte HTTP streamable

#### Scenario: Valor inválido
- **WHEN** la variable `DOCMOST_MCP_TRANSPORT` tiene un valor distinto de `stdio` o `http`
- **THEN** la aplicación falla al arrancar con un mensaje de error que indica los valores aceptados

### Requirement: Puerto HTTP configurable

El servidor, cuando se ejecuta en modo HTTP, DEBE (MUST) escuchar en el puerto especificado por `DOCMOST_MCP_PORT`. Si la variable no está definida, DEBE (MUST) usar el puerto `3001` por defecto. El valor DEBE (MUST) ser un entero entre 1 y 65535.

#### Scenario: Puerto explícito
- **WHEN** la variable `DOCMOST_MCP_PORT=8080` está definida y el modo es `http`
- **THEN** el servidor HTTP escucha en el puerto 8080

#### Scenario: Puerto por defecto
- **WHEN** la variable `DOCMOST_MCP_PORT` no está definida y el modo es `http`
- **THEN** el servidor HTTP escucha en el puerto 3001

#### Scenario: Puerto fuera de rango
- **WHEN** la variable `DOCMOST_MCP_PORT` tiene un valor menor que 1 o mayor que 65535
- **THEN** la aplicación falla al arrancar con un mensaje de error

### Requirement: Validación de credenciales al arranque

El servidor DEBE (MUST) validar al arrancar que las variables `DOCMOST_URL`, `DOCMOST_EMAIL` y `DOCMOST_PASSWORD` están definidas y no vacías. Si falta alguna, la aplicación DEBE (MUST) fallar al arrancar con un mensaje de error que indique exactamente qué variable falta.

#### Scenario: Todas las credenciales presentes
- **WHEN** `DOCMOST_URL`, `DOCMOST_EMAIL` y `DOCMOST_PASSWORD` están definidas y no vacías
- **THEN** la aplicación arranca con éxito y queda a la espera de conexiones

#### Scenario: Falta URL
- **WHEN** `DOCMOST_URL` no está definida
- **THEN** la aplicación falla al arrancar indicando que falta `DOCMOST_URL`

#### Scenario: Falta email
- **WHEN** `DOCMOST_EMAIL` no está definida
- **THEN** la aplicación falla al arrancar indicando que falta `DOCMOST_EMAIL`

#### Scenario: Falta password
- **WHEN** `DOCMOST_PASSWORD` no está definida
- **THEN** la aplicación falla al arrancar indicando que falta `DOCMOST_PASSWORD`
