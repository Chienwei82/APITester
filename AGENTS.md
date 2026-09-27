# Guía para agentes de código — Archivo de endpoints de APITester

Esta guía está dirigida a agentes de código (Copilot, OpenCode, Pi, Claude, etc.) que
necesiten **crear, modificar o validar archivos de configuración de endpoints** para
APITester. Describe el formato exacto del JSON, las reglas de validación que impone el
código, y los errores comunes a evitar. Si generás un archivo que sigue esta guía,
la herramienta lo ejecuta sin advertencias.

---

## 1. Qué es APITester

Herramienta CLI en .NET (`APITester.Rest`) que ejecuta requests HTTP definidos en un
archivo JSON, en paralelo, y guarda cada respuesta (status, headers, body, timing,
headers enviados) en archivos de salida.

```bash
cd APITester.Rest
dotnet run                          # usa rest-config.json del directorio actual
dotnet run -- -c mi-config.json     # archivo explícito
```

El archivo de endpoints **por defecto es `rest-config.json`** (constante única:
`CliArgs.DefaultConfigFile` en `APITester.Core/Models/CliArgs.cs`). La ruta es relativa
al directorio de trabajo desde donde se ejecuta el proceso, no al proyecto.

---

## 2. Formas válidas del archivo

El loader (`APITester.Rest/Services/RestConfigLoader.cs`) acepta exactamente tres formas.
La detección es por estructura, no por extensión:

### A. Array de requests (recomendado para varios endpoints)

```json
[
  {
    "name": "Obtener usuarios",
    "url": "https://api.ejemplo.com/users",
    "method": "GET",
    "output": "respuestas/usuarios.json"
  },
  {
    "name": "Crear usuario",
    "url": "https://api.ejemplo.com/users",
    "method": "POST",
    "headers": { "Content-Type": "application/json" },
    "body": "{\"name\": \"Juan\"}",
    "output": "respuestas/creado.json"
  }
]
```

### B. Objeto único con `url`

```json
{
  "name": "Ping",
  "url": "https://api.ejemplo.com/health",
  "method": "GET"
}
```

### C. Objeto con `defaults` + `requests` (y opcionalmente `request` singular)

```json
{
  "defaults": {
    "baseUrl": "https://api.ejemplo.com",
    "headers": { "Authorization": "Bearer ${TOKEN_API}" },
    "timeout": 15,
    "retries": 2,
    "retryDelayMs": 1000,
    "retryExponentialBackoff": true,
    "retryOnStatusCodes": [429, 500, 502, 503, 504],
    "maxBodyBytes": 4194304
  },
  "requests": [
    { "name": "Usuarios", "url": "/users", "method": "GET" },
    { "name": "Productos", "url": "/products", "method": "GET" }
  ]
}
```

**Reglas de detección (importantes):**

- Un **objeto** que contenga alguna de las claves `defaults`, `requests` o `request`
  se interpreta como archivo de configuración (forma C). Un objeto con `requests: []`
  vacío y sin `request` es un **error** ("JSON sin requests").
- Un **objeto** sin esas claves debe tener `url` para ser un request válido (forma B).
- Un **array** acepta items sin `url` (se validan después como advertencia), pero un
  array vacío `[]` es error.
- Escalares (string, número) como raíz son error.
- Límite de tamaño del archivo: **10 MB**.

**Reglas de `defaults`:** un valor explícito en el request **siempre gana** al default.
Los defaults se aplican solo cuando el campo del request es `null`/ausente.
`baseUrl` se antepone solo cuando la URL del request **no empieza con `http`**.

---

## 3. Referencia de campos por request

Modelo: `APITester.Rest/Models/RestRequestConfig.cs`

| Campo | Tipo | Obligatorio | Defecto | Descripción |
|---|---|---|---|---|
| `name` | string | no | — | Etiqueta para consola y salida |
| `url` | string | **sí** | — | URL absoluta `http`/`https`, o relativa si hay `baseUrl` |
| `method` | string | no | `GET` | GET, POST, PUT, PATCH, DELETE, HEAD, OPTIONS |
| `headers` | mapa string→string | no | — | Headers HTTP (ver §6 restricciones) |
| `query` | mapa string→string | no | — | Query string; se URL-escapea clave y valor |
| `body` | string | no | — | Cuerpo **como string** (ver §5). Solo POST/PUT/PATCH |
| `cert.path` / `cert.password` | string | no | — | Certificado cliente TLS (.pfx PKCS#12) |
| `output` | string | no | `rest-response.json` o `-o` | Archivo de salida del request |
| `appendOutput` | bool | no | `false` | Agregar (NDJSON) al final en vez de sobrescribir |
| `timeout` | int | no | `30` | Segundos, entre **1 y 300** |
| `retries` | int | no | `0` | Reintentos, entre **0 y 10** |
| `retryDelayMs` | int | no | `1000` | Espera base entre reintentos, **0–60000** |
| `retryExponentialBackoff` | bool | no | `false` | Backoff exponencial con jitter (cap 30s) |
| `retryOnStatusCodes` | lista de int | no | — | Reintentar también ante estos status |
| `maxBodyBytes` | long | no | `4194304` | Límite de lectura del body de respuesta; `0` desactiva la lectura |

Valores efectivos (propiedades `Effective*` del modelo): timeout 30s, retries 0,
retryDelayMs 1000, backoff false, maxBodyBytes 4 MB.

### Defaults disponibles (`defaults`)

`baseUrl`, `headers`, `query`, `timeout`, `retries`, `retryDelayMs`,
`retryExponentialBackoff`, `retryOnStatusCodes`, `maxBodyBytes`.

---

## 4. Reglas de validación (se aplican SIEMPRE)

`RestRequestConfig.Validate()` produce **advertencias** (no detienen la ejecución, salvo
`--strict`). Aparecen como `ADVERTENCIA: [N] <mensaje>` en consola.

| Regla | Límite |
|---|---|
| `url` | Absoluta, esquema `http` o `https` (`Uri.TryCreate` + check de esquema) |
| `timeout` | 1–300 segundos |
| `retries` | 0–10 |
| `retryDelayMs` | 0–60000 |
| `method` | Solo: GET, POST, PUT, PATCH, DELETE, HEAD, OPTIONS |
| `body` | Prohibido en GET, DELETE, HEAD, OPTIONS |
| `cert.path` | El archivo debe existir en disco |

Con `--strict`, cualquier advertencia aborta la ejecución con exit code 1.

**Errores duros en ejecución** (no advertencias; el request termina con campo `Error`):

- Header de la lista prohibida: `Content-Length`, `Transfer-Encoding`, `Host`,
  `Connection`, `Upgrade`, `Proxy-Connection`, `Keep-Alive`, `TE`, `Trailer`.
- Valor de header con `\r` o `\n` (header injection).

`Content-Type` de `headers` se ignora como header directo: se usa para construir el
content del body (default `application/json`).

---

## 5. Body

- El body es **siempre un string JSON-escapado** dentro del config, aunque su contenido
  sea JSON. Mal ejemplo: `"body": {"name": "Juan"}`. Correcto:
  `"body": "{\"name\": \"Juan\"}"`.
- Se envía solo en POST/PUT/PATCH (ver `HttpMethods.SupportsBody`).
- Para formularios: `"body": "a=1&b=2"` con header `Content-Type: application/x-www-form-urlencoded`.
- Si el body contiene `${VAR}`, se resuelve antes de enviar.

---

## 6. Variables de entorno

Sintaxis en **cualquier parte de `url`, `headers`, `query` y `body`**:

- `${NOMBRE_VAR}` — reemplaza por la variable de entorno.
- `${NOMBRE_VAR:-valor-default}` — usa el default si la variable no existe o es vacía.

Si la variable no existe y no hay default, el placeholder queda **sin reemplazar**
(texto literal `${NOMBRE_VAR}`), lo que típicamente produce URL inválida. Los agentes
deben preferir siempre la forma con default o garantizar que la variable exista.

> Nota: el envío HTTP real viaja con los valores resueltos; lo que se redacta/persiste
> es la **salida** (ver §8).

---

## 7. Salidas

- `output` por request gana sobre el flag global `-o`. Sin ninguno: `rest-response.json`.
- Los directorios se crean automáticamente.
- **Varios requests con el mismo `output` (sin append)** se agrupan y se escriben **una
  sola vez** como array JSON (no se pisan entre sí).
- `appendOutput: true` agrega cada respuesta como línea NDJSON al final del archivo.
  Combinación válida: un request sobrescribe `out.json` y otro con el mismo `output` +
  `appendOutput` le agrega líneas.
- `--format ndjson` escribe cada respuesta del grupo como una línea (case-insensitive:
  `NDJSON` también vale).

---

## 8. Redacción de credenciales en la salida

Por defecto, en los archivos de salida estos headers se guardan como `"***"`:

`Authorization`, `Proxy-Authorization`, `Cookie`, `Set-Cookie`.

El request HTTP real siempre viaja con el valor completo. Para persistirlos sin
enmascarar (solo en entorno controlado): flag `--no-redact`.

---

## 9. Exit codes y reintentos

| Código | Significado |
|---|---|
| `0` | Todos los requests exitosos: sin error de transporte y status `< 400` |
| `1` | Error de argumentos/config/escritura, o al menos un request fallido (error de red **o status ≥ 400**) |
| `130` | Cancelado por el usuario (Ctrl+C) |

Reintentos: ante `HttpRequestException`, `TaskCanceledException`,
`OperationCanceledException` (no cancelaciones externas) y ante los status listados en
`retryOnStatusCodes`. Si todos los intentos fallan, el `Error` incluye la causa real
del último intento. Backoff exponencial: `delayMs * 2^intento + jitter`, con cap de 30s.

---

## 10. Errores comunes a evitar (checklist del agente)

1. ❌ `body` como objeto JSON nativo → el modelo espera **string**. Escapar comillas.
2. ❌ `body` en GET/DELETE → advertencia y el body no se envía.
3. ❌ `timeout: 500` → fuera de rango (máx 300).
4. ❌ `retries: 50` → fuera de rango (máx 10). Con backoff exponencial, 10 retries ya
   saturan el cap de 30s por intento.
5. ❌ URL relativa sin `baseUrl` → advertencia de URL inválida.
6. ❌ URL con placeholder `${VAR}` sin resolver → la URL queda literal y falla.
7. ❌ Headers prohibidos (`Host`, `Content-Length`, ...) → error duro por request.
8. ❌ Objeto raíz con `requests: []` y sin `request` → error "JSON sin requests".
9. ❌ Query con valores que necesitan escape: el tool escapa automáticamente; **no**
   pre-escapar (doble escape).
10. ❌ Asumir que status 4xx/5xx es "éxito": para CI, un 404 hace exit code 1.
11. ✅ Nombrar cada request con `name`: identifica la salida y el progreso en consola.
12. ✅ `output` con subdirectorios (`respuestas/x.json`): se crean solos.

---

## 11. Cómo verificar un archivo generado

```bash
# 1. Validación y ejecución reales (desde el repo):
cd APITester.Rest
dotnet run -- -c /ruta/al/archivo.json --strict   # --strict convierte advertencias en error

# 2. Ver ayuda de flags:
dotnet run -- --help

# 3. Suite de tests (incluye loader, validación, parser):
dotnet test
```

Ejemplo completo de referencia (endpoints públicos reales):
`APITester.Rest/rest-config.json`.

---

## 12. Mapa del código relevante

| Tema | Archivo |
|---|---|
| Modelo y validación del request | `APITester.Rest/Models/RestRequestConfig.cs` |
| Detección de estructura y defaults | `APITester.Rest/Services/RestConfigLoader.cs` |
| Loader genérico (array / objeto único) | `APITester.Core/Services/GenericConfigLoader.cs` |
| Resolución de variables de entorno | `APITester.Core/Services/EnvVarResolver.cs` |
| Construcción del request HTTP | `APITester.Rest/Services/RequestBuilder.cs` |
| Validadores reutilizables | `APITester.Core/Services/ConfigValidator.cs` |
| Ejecución, retries y límites de body | `APITester.Rest/Services/HttpExecutor.cs`, `APITester.Core/Services/RetryPolicy.cs` |
| Agrupado de salidas | `APITester.Rest/Services/RestOrchestrator.cs` (`BuildWritePlan`) |
| Redacción de headers | `APITester.Core/Services/HeaderRedactor.cs` |
| Parser de CLI | `APITester.Core/Services/ArgumentParser.cs` |

Cambios en la documentación de usuario viven en `README.md`; esta guía (`AGENTS.md`) es
la fuente de verdad operativa para agentes.
