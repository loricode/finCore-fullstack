# Seguridad y acceso

## Endpoints

| Método | Ruta | Permiso |
|---|---|---|
| POST | `/api/v1/auth/login` | anónimo |
| POST | `/api/v1/auth/refresh` | anónimo (refresh token) |
| POST | `/api/v1/auth/logout` | autenticado |
| GET | `/api/v1/auth/me` | autenticado — usuario, permisos y sucursales autorizadas |
| GET, POST | `/api/v1/users` | `users.manage` — POST `{ userName, email, fullName, password, roles[], branchIds[] }` → 201 |
| GET, PUT | `/api/v1/users/{id}` | `users.manage` — PUT `{ email, fullName, roles[], branchIds[], rowVersion }` |
| PUT | `/api/v1/users/{id}/branches` | `users.manage` — `{ branchIds[] }` (HU-04) |
| POST | `/api/v1/users/{id}/deactivate` · `/api/v1/users/{id}/activate` | `users.manage` |
| GET | `/api/v1/roles` | `users.manage` — roles con permisos y `grantsAllBranches` |
| GET, POST, PUT | `/api/v1/branches` · `/api/v1/accounts` | `branches.manage` · `accounts.manage` (lectura: autenticado, filtrada por alcance) |

## Requisito: autenticación con tokens de corta duración
La API DEBE emitir un access token JWT de 15 minutos (HMAC-SHA256, claims `sub`, `unique_name`, `cid` y un `perm` por permiso)
y un refresh token opaco de 7 días. Del refresh token solo se guarda su SHA-256.

El refresh token NO DEBE viajar en el cuerpo: va en la cookie `fincore_rt` con `HttpOnly`, `Secure`, `SameSite=Strict` y
`Path=/api/v1/auth`. El access token lo guarda Angular solo en memoria; al recargar la página se obtiene uno nuevo con `POST /auth/refresh`.

### Escenario: login correcto
- **Dado** un usuario activo y no bloqueado con la contraseña correcta
- **Cuando** envía `POST /auth/login`
- **Entonces** recibe 200 con `{ accessToken, expiresAtUtc }` y la cookie `fincore_rt`; `FailedLoginCount` vuelve a 0; se audita `LOGIN`

### Escenario: mantener sesión iniciada
- **Dado** `rememberMe = false` en el login
- **Entonces** la cookie `fincore_rt` no tiene `Expires` (el navegador la borra al cerrarse) y cada renovación conserva esa elección (cookie `fincore_rp`)
- **Por defecto** `rememberMe = true`: la cookie vence con el refresh token (7 días)

### Escenario: demasiados intentos desde una IP
- **Dado** más de 20 intentos de login por minuto desde la misma IP
- **Entonces** 429 (limitador de la API, independiente del bloqueo por usuario)

### Escenario: login incorrecto
- **Dado** un usuario inexistente o una contraseña incorrecta
- **Entonces** la respuesta es 401 con el mismo mensaje en ambos casos y se audita `LOGIN_FAILED` (sin la contraseña)

### Escenario: bloqueo
- **Dado** 5 intentos fallidos consecutivos
- **Entonces** `LockoutEndUtc` = ahora + 15 min y el login responde 423 hasta esa hora

### Escenario: rotación y reutilización
- **Cuando** se usa un refresh token válido
- **Entonces** se emite uno nuevo en la cookie, el anterior queda revocado con `ReplacedByTokenHash` y se audita `TOKEN_REFRESHED`
- **Y** los permisos del nuevo access token se leen de nuevo (un cambio de roles se aplica a más tardar en 15 minutos)
- **Dado** un refresh token ya revocado
- **Cuando** se intenta usar
- **Entonces** 401, se revocan todos los tokens activos del usuario (posible robo) y se audita `REFRESH_TOKEN_REUSE`

### Escenario: cierre de sesión
- **Cuando** se envía `POST /auth/logout`
- **Entonces** 204, el refresh token queda revocado, la cookie se borra y se audita `LOGOUT`; repetirlo también responde 204

## Requisito: base de datos con mínimo privilegio
La API DEBE conectarse con un login miembro de `fincore_app_role` (`database/010_Security`): lectura y escritura en tablas de negocio,
`EXECUTE` en `rec`, `stg`, `etl` y `rpt`, sin `DELETE` y sin DDL.

## Requisito: autorización por permisos
Cada endpoint DEBE exigir un permiso (`sec.Permissions.Code`), no un rol. Los roles solo agrupan permisos.

### Escenario: sin permiso
- **Dado** un operador
- **Cuando** llama `POST /api/v1/transactions/{id}/process`
- **Entonces** 403 `application/problem+json`

## Requisito: alcance por sucursal
Toda consulta y escritura sobre datos de sucursal DEBE limitarse a las sucursales autorizadas (`sec.tvf_AuthorizedBranches`):
las asignadas en `sec.UserBranches`, o todas las de su empresa con `branches.all`. Nunca otra empresa.

### Escenario: operador de Montería
- **Dado** `operador.monteria` asignado solo a Montería
- **Cuando** lista operaciones sin filtros
- **Entonces** solo recibe operaciones de Montería
- **Cuando** consulta una operación de Bogotá por su id
- **Entonces** recibe 404 (no se revela que existe)
- **Cuando** registra una operación en Bogotá
- **Entonces** recibe 403

### Escenario: usuario inactivo
- **Dado** un usuario desactivado
- **Entonces** `sec.tvf_AuthorizedBranches` no devuelve sucursales y sus tokens se rechazan

## Requisito: administración de usuarios (HU-02, HU-04)
Los usuarios se crean en la empresa del administrador; un usuario de otra empresa responde 404.

| Regla | Respuesta |
|---|---|
| Usuario (3–50, minúsculas, números, `.`, `-`, `_`) o correo repetido | 409 |
| Rol inexistente o sucursal inactiva / de otra empresa | 400 en `roles` / `branchIds` |
| Roles sin `branches.all` y ninguna sucursal | 400 en `branchIds` |
| Editar con un `rowVersion` viejo | 409 |
| Quitarse a sí mismo `users.manage`, o desactivarse | 409 |
| Desactivar | revoca todos los refresh tokens del usuario |
| Activar | levanta también un bloqueo por intentos fallidos |

Cada cambio se audita (`USER_CREATED`, `USER_UPDATED`, `USER_BRANCHES_CHANGED`, `USER_ACTIVATED`, `USER_DEACTIVATED`)
con valores antes y después. Crear un usuario es atómico: usuario, roles, sucursales y auditoría en una transacción.

## Requisito: contraseñas
Las contraseñas DEBEN guardarse con `PasswordHasher` de ASP.NET Core Identity (PBKDF2-SHA512, formato v3) y tener al menos
10 caracteres con mayúscula, minúscula, número y símbolo. Nunca se registran en logs ni en auditoría.
