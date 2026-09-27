# APITester

Herramienta portable de linea de comandos para ejecutar requests HTTP contra APIs REST y guardar las respuestas en un archivo JSON.

Lee una configuracion en JSON (uno o varios requests), los ejecuta uno por uno, en el orden del archivo, y produce un archivo con el detalle de cada respuesta: status code, headers, body, tiempo de respuesta, tamano, y headers enviados en el request.

Requiere .NET SDK 10.0+ ([descargar](https://dotnet.microsoft.com/download)).

---

## Uso rapido

### Modo interactivo

```bash
cd APITester.Rest
dotnet run
```

Sin argumentos, la app abre un menu TUI en consola (colores, navegacion con **flechas** y Enter):

1. **Ejecutar requests** — ejecuta la configuracion seleccionada y vuelve al menu.
2. **Elegir archivo de configuracion** — cambia el JSON de requests.
3. **Opciones de ejecucion** — salida, formato, verbose, strict, quiet y sin colores.
4. **Ejecutar un request (paso a paso)** — asistente que te pregunta todos los parametros.
5. **Ver ayuda**
0. **Salir**

Ctrl+C durante una ejecucion cancela solo esa ejecucion y te devuelve al menu; en un prompt del menu la aplicacion sale directamente. Si la entrada esta redirigida (pipes, CI), sin argumentos se ejecuta directamente `rest-config.json` para no bloquear scripts.

### Asistente paso a paso

La opcion **4** construye un request sin tocar el JSON: pregunta nombre, metodo, URL, headers, query string, body (solo en POST/PUT/PATCH), timeout, reintentos, archivo de salida y, si quieres, la configuracion avanzada (delay, backoff, limite de respuesta y certificado cliente). Al final muestra un resumen y pide confirmacion; el request se ejecuta con el mismo pipeline que la CLI (spinner, validaciones, retries y guardado si indicas archivo).

### Linea de comandos

Para ejecutar sin menu, pasa argumentos (despues de `--`):

```
dotnet run -- -c archivo.json [-o salida.json] [-v] [--format json|ndjson] [--strict] [--quiet] [--no-color] [-h]
```

Con `-c rest-config.json` (o sin `-c`, usando ese archivo) ejecuta los requests definidos ahi, guardando cada respuesta en su archivo `output`.

| Argumento | Descripcion |
|---|---|
| `-c`, `--config <archivo>` | Ruta al archivo JSON con la configuracion de requests (default: `rest-config.json`). Soporta `--config=<archivo>` |
| `-o`, `--output <archivo>` | Archivo de salida por defecto (si un request no define `output`). Soporta `--output=<archivo>` |
| `-v`, `--verbose` | Muestra detalles adicionales (query, body, certificado, reintentos) |
| `--format json\|ndjson` | Formato de salida: `json` (indentado) o `ndjson` (una linea por respuesta) |
| `--strict` | Fallar si hay advertencias de validacion |
| `--quiet` | Solo mostrar errores y el resumen final |
| `--no-color` | Deshabilitar salida con colores |
| `-h`, `--help` | Muestra la ayuda |

---

## Ejemplo minimo

Un `rest-config.json` minimo con un solo request:

```json
{
  "name": "Obtener usuarios",
  "url": "https://jsonplaceholder.typicode.com/users",
  "method": "GET",
  "output": "respuestas/usuarios.json"
}
```

Ejecutalo asi:

```bash
dotnet run -- -c rest-config.json
```

---

## Formato del archivo JSON

Puede ser un request unico, una lista de requests, o un objeto con `defaults` y `requests`.

### Request simple

```json
{
  "name": "Obtener usuarios",
  "url": "https://jsonplaceholder.typicode.com/users",
  "method": "GET",
  "output": "usuarios.json"
}
```

### Varios requests

```json
[
  {
    "name": "Listar usuarios",
    "url": "https://api.ejemplo.com/users",
    "method": "GET",
    "output": "usuarios.json"
  },
  {
    "name": "Crear usuario",
    "url": "https://api.ejemplo.com/users",
    "method": "POST",
    "headers": {
      "Content-Type": "application/json",
      "Authorization": "Bearer ${TOKEN_API}"
    },
    "body": "{\"name\": \"Juan Perez\", \"email\": \"juan@example.com\"}",
    "output": "crear-usuario.json"
  }
]
```

### Defaults compartidos

```json
{
  "defaults": {
    "baseUrl": "https://api.ejemplo.com",
    "headers": {
      "Authorization": "Bearer ${TOKEN_API}"
    },
    "timeout": 15,
    "retries": 2
  },
  "requests": [
    {
      "name": "Usuarios",
      "url": "/users",
      "method": "GET"
    },
    {
      "name": "Productos",
      "url": "/products",
      "method": "GET"
    }
  ]
}
```

- `baseUrl`: las URLs relativas de los requests se resuelven contra esta base
- `headers` / `query`: se aplican a los requests que no definan los propios
- `timeout`, `retries`, `retryDelayMs`: se aplican solo a los requests que **no** definen un valor propio (así un valor explícito en el request siempre gana al default)

### Campos disponibles

| Campo | Tipo | Obligatorio | Defecto | Descripcion |
|---|---|---|---|---|
| `name` | string | no | `"{method} {url}"` | Nombre descriptivo del request |
| `url` | string | **si** | — | URL del endpoint |
| `method` | string | no | `GET` | Metodo HTTP (GET, POST, PUT, PATCH, DELETE, HEAD, OPTIONS) |
| `headers` | mapa string:string | no | — | Headers HTTP |
| `query` | mapa string:string | no | — | Parametros de query string |
| `body` | string | no | — | Cuerpo del request (para POST/PUT/PATCH) |
| `output` | string | no | `rest-response.json` | Archivo donde guardar la respuesta |
| `appendOutput` | bool | no | `false` | Agregar la respuesta al final del archivo en vez de sobrescribirlo |
| `timeout` | int | no | `30` | Timeout en segundos (max 300) |
| `retries` | int | no | `0` | Cantidad de reintentos ante fallos transitorios |
| `retryDelayMs` | int | no | `1000` | Espera entre reintentos en milisegundos |
| `retryExponentialBackoff` | bool | no | `false` | Usar backoff exponencial (con jitter) en los reintentos |
| `retryOnStatusCodes` | lista de int | no | — | Reintentar tambien cuando la respuesta tenga estos status codes |
| `cert.path` | string | no | — | Ruta a un certificado cliente (.pfx) |
| `cert.password` | string | no | — | Contrasena del certificado |

### Validaciones

- `url`: debe ser una URL absoluta valida con http o https
- `timeout`: entre 1 y 300 segundos
- `method`: solo metodos HTTP estandar
- `cert.path`: si se especifica, el archivo debe existir en disco
- `body`: no se permite en metodos sin cuerpo (GET, DELETE, HEAD, OPTIONS)

Las advertencias de validacion aparecen como `ADVERTENCIA` en consola y la ejecucion continua, salvo con `--strict`.

---

## Variables de entorno

Se puede usar la sintaxis `${NOMBRE_VAR}` en cualquier campo del JSON y APITester la reemplaza con el valor de la variable de entorno correspondiente. Soporta default: `${VAR:-default}`.

```json
{
  "headers": {
    "Authorization": "Bearer ${TOKEN_API}"
  },
  "url": "https://${HOST:-localhost}:8080/api/v1/users"
}
```

Esto funciona en: `url`, `headers`, `query`, `body`, y `cert.password`.

---

## Output JSON

El resultado se guarda en un archivo JSON con la siguiente estructura:

```json
{
  "Request": {
    "Name": "Obtener usuarios",
    "Url": "https://jsonplaceholder.typicode.com/users",
    "Method": "GET",
    "RequestHeaders": {
      "Authorization": "Bearer token123"
    }
  },
  "Response": {
    "StatusCode": 200,
    "StatusText": "OK",
    "Headers": {
      "Content-Type": "application/json",
      "...": "..."
    },
    "Body": { "...": "..." },
    "BodyRaw": "{\n  ...\n}",
    "TimeMs": 430,
    "SizeBytes": 5645
  }
}
```

- Si el request falla (error de red, timeout, etc.), el campo `Error` contiene el mensaje y `Response` es `null`.
- Si la respuesta es JSON valido, `Body` se parsea como objeto JSON. El texto crudo siempre esta disponible en `BodyRaw`.
- `RequestHeaders` contiene los headers que fueron enviados en el request (incluyendo los resueltos desde variables de entorno).

Los directorios de salida (por ejemplo `respuestas/`) se crean automaticamente si no existen.

---

## Certificados cliente TLS

Para APIs que requieren autenticacion mutua TLS (mTLS):

```json
{
  "cert": {
    "path": "/home/usuario/certificados/cliente.pfx",
    "password": "${CERT_PASSWORD}"
  }
}
```

Soporta archivos `.pfx` (PKCS#12). Si solo se necesita un certificado sin contrasena, se puede omitir `password`. Los handlers se cachean por certificado.

---

## Reintentos (retry)

Cuando un request falla por `HttpRequestException`, `TaskCanceledException` o `OperationCanceledException`, APITester puede reintentar automaticamente. Las cancelaciones externas (por ejemplo, Ctrl+C) no se reintentan.

```json
{
  "retries": 3,
  "retryDelayMs": 2000,
  "retryExponentialBackoff": true,
  "retryOnStatusCodes": [429, 500, 502, 503, 504]
}
```

Si todos los intentos fallan, se devuelve el error del ultimo intento.

---

## Arquitectura

```
APITester.slnx
├── APITester.Core           ← Logica compartida (modelos, interfaces, utilidades)
│   ├── Models/              ← ApiResponse, CertConfig, CliArgs, ExecutionSummary
│   └── Services/            ← ConfigValidator, ConsolePresenter, EnvVarResolver,
│                               GenericConfigLoader, HeaderRules, HttpMethods, JsonFormatter,
│                               RetryPolicy, etc.
├── APITester.Rest           ← Implementacion REST
│   ├── Program.cs           ← Punto de entrada (CLI o modo interactivo)
│   ├── Models/              ← RestRequestConfig, RestConfigDefaults
│   └── Services/            ← RestOrchestrator, InteractiveSession, RequestWizard, Tui,
│                               CtrlCCoordinator, HttpExecutor, RestConfigLoader, RequestExecutor
└── APITester.Tests          ← Tests unitarios
```

La UI del modo interactivo usa [Spectre.Console](https://spectreconsole.net/) (menus, prompts y colores) a traves de `IAnsiConsole`, lo que permite probarlo en memoria con `TestConsole`; la CLI y el `ConsolePresenter` no dependen de el.

El diseno separa el nucleo (`Core`) del protocolo especifico (`Rest`), lo que permite agregar otros protocolos (GraphQL, gRPC, etc.) implementando `IApiExecutor<T>`.

---

## Scripts

Utilidades de mantenimiento del repo en [`scripts/`](scripts/README.md), por ejemplo la limpieza de ramas locales despues de mergear un PR:

```bash
python3 scripts/git_cleanup.py --dry-run
```

---

Repositorio: https://github.com/Chienwei82/APITester
