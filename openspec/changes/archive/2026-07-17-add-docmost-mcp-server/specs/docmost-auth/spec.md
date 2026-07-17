## ADDED Requirements

### Requirement: Login transparente con email y password

El cliente HTTP de Docmost DEBE (MUST) autenticarse enviando `POST {DOCMOST_URL}/api/auth/login` con el body `{"email": "...", "password": "..."}`. Si la respuesta es 200, DEBE (MUST) almacenar la cookie `Set-Cookie` devuelta por el servidor en un `CookieContainer` compartido. El login DEBE (MUST) ser **transparente** para el LLM: ningún tool MCP lo invoca.

#### Scenario: Login exitoso
- **WHEN** se llama a `POST /api/auth/login` con credenciales válidas
- **THEN** el cliente recibe 200 y la cookie se almacena en el `CookieContainer` para uso posterior

#### Scenario: Login con credenciales inválidas
- **WHEN** se llama a `POST /api/auth/login` con email o password incorrectos
- **THEN** el cliente recibe 401 y el `CookieSessionStore` marca el estado como "no autenticable" con un mensaje legible

### Requirement: Re-login automático ante 401

Cuando cualquier tool recibe un 401 de Docmost, el cliente DEBE (MUST) reintentar el login una sola vez y reenviar la request original. Si la respuesta del re-login es exitosa pero la request reenviada vuelve a recibir 401, el cliente DEBE (MUST) propagar el error al tool.

#### Scenario: Cookie expirada y credenciales válidas
- **WHEN** una request autenticada recibe 401 y las credenciales son válidas
- **THEN** el cliente re-loguea automáticamente, obtiene una nueva cookie y reenvía la request con la nueva cookie, devolviendo la respuesta al tool

#### Scenario: Cookie expirada y credenciales inválidas
- **WHEN** una request autenticada recibe 401 y el re-login devuelve 401
- **THEN** el cliente propaga al tool un error claro indicando que las credenciales de Docmost son inválidas y debe corregirse `DOCMOST_EMAIL` o `DOCMOST_PASSWORD`

### Requirement: Serialización de logins concurrentes

Cuando N requests detectan la cookie caducada al mismo tiempo, el cliente DEBE (MUST) ejecutar **un solo** login y hacer esperar al resto. Esto evita sobrecargar al servidor de Docmost con logins paralelos y reduce la probabilidad de rate-limiting.

#### Scenario: Cinco requests concurrentes con cookie caducada
- **WHEN** cinco tools ejecutan requests en paralelo y todos reciben 401 simultáneamente
- **THEN** el cliente ejecuta una única llamada a `POST /api/auth/login`; los otros cuatro esperan el resultado y reutilizan la nueva cookie

#### Scenario: Login en curso y llega otra request
- **WHEN** un login está en curso y otra request intenta ejecutarse
- **THEN** la segunda request espera a que el login termine (con un timeout configurable, por defecto 10 segundos) antes de proceder

### Requirement: Sin tool MCP de autenticación

El servidor NO DEBE (MUST) exponer ningún tool MCP relacionado con autenticación (ni `auth_login`, ni `auth_status`, ni `auth_logout`). El proceso de autenticación es completamente interno. El LLM solo interactúa con los tools de spaces, pages y search.

#### Scenario: Listado de tools no incluye autenticación
- **WHEN** un cliente MCP pide la lista de tools
- **THEN** el listado contiene únicamente los tools de spaces, pages y search; ningún tool expone credenciales o estado de sesión
