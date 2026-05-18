# APITester

[![.NET CI](https://github.com/Chienwei82/APITester/actions/workflows/dotnet.yml/badge.svg)](https://github.com/Chienwei82/APITester/actions/workflows/dotnet.yml)
[![Release](https://github.com/Chienwei82/APITester/actions/workflows/release.yml/badge.svg)](https://github.com/Chienwei82/APITester/releases)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

Herramienta portable de linea de comandos para ejecutar requests HTTP contra APIs REST y guardar las respuestas en un archivo JSON.

Lee una configuracion en JSON (uno o varios requests), los ejecuta en paralelo con un limite de concurrencia, y produce un archivo con el detalle de cada respuesta: status code, headers, body, tiempo de respuesta, tamano, y headers enviados en el request.

---

## Instalacion

```bash
git clone <repo>
cd APITester
dotnet build
```

Requiere .NET SDK 10.0+ ([descargar](https://dotnet.microsoft.com/download)).

---

## Uso rapido

El proyecto incluye un ejemplo funcional que consulta JSONPlaceholder:

```bash
cd APITester.Rest
dotnet run
```

Esto usa `rest-config.json` por defecto, hace un GET a `https://jsonplaceholder.typicode.com/users` y guarda el resultado en `usuarios.json`.

### Linea de comandos

```
dotnet run -- -c archivo.json [-o salida.json] [-v] [-h]
```

| Argumento | Descripcion |
|---|---|
| `-c`, `--config` | Ruta al archivo JSON con la configuracion de requests |
| `-o`, `--output` | Archivo de salida (opcional, si no se especifica usa el `output` del primer request) |
| `-v`, `--verbose` | Muestra detalles adicionales (query, body, certificado, reintentos) |
| `-h`, `--help` | Muestra la ayuda |

---

## Formato del archivo JSON

Puede ser un request unico o una lista de requests.

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
  },
  {
    "name": "Eliminar usuario",
    "url": "https://api.ejemplo.com/users/123",
    "method": "DELETE",
    "output": "eliminar.json"
  }
]
```

### Campos disponibles

| Campo | Tipo | Obligatorio | Defecto | Descripcion |
|---|---|---|---|---|
| `name` | string | no | `"{method} {url}"` | Nombre descriptivo del request |
| `url` | string | **si** | — | URL del endpoint |
| `method` | string | no | `GET` | Metodo HTTP (GET, POST, PUT, PATCH, DELETE, HEAD, OPTIONS) |
| `headers` | mapa string:string | no | — | Headers HTTP |
| `query` | mapa string:string | no | — | Parametros de query string |
| `body` | string | no | — | Cuerpo del request (para POST/PUT/PATCH) |
| `cert.path` | string | no | — | Ruta a un certificado cliente (.pfx) |
| `cert.password` | string | no | — | Contrasena del certificado |
| `output` | string | no | `rest-response.json` | Archivo donde guardar la respuesta |
| `timeout` | int | no | `30` | Timeout en segundos (max 300) |
| `retries` | int | no | `0` | Cantidad de reintentos ante fallos transitorios |
| `retryDelayMs` | int | no | `1000` | Espera entre reintentos en milisegundos |

### Validaciones

- `url`: debe ser una URL absoluta valida con http o https
- `timeout`: entre 1 y 300 segundos
- `method`: solo metodos HTTP estandar
- `cert.path`: si se especifica, el archivo debe existir en disco

Los errores de validacion aparecen como `ADVERTENCIA` en consola pero la ejecucion continua (aunque puede fallar en runtime si los datos son invalidos).

---

## Variables de entorno

Se puede usar la sintaxis `${NOMBRE_VAR}` en cualquier campo del JSON y APITester la reemplaza con el valor de la variable de entorno correspondiente. Si la variable no existe, se deja el texto original.

```json
{
  "headers": {
    "Authorization": "Bearer ${TOKEN_API}"
  },
  "url": "https://${HOST}/api/v1/users"
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
      ...
    },
    "Body": { ... },
    "BodyRaw": "{\n  ...\n}",
    "TimeMs": 430,
    "SizeBytes": 5645
  }
}
```

Si el request falla (error de red, timeout, etc.), el campo `Error` contiene el mensaje y `Response` es `null`.

Si la respuesta es JSON valido, `Body` se parsea como objeto JSON. El texto crudo siempre esta disponible en `BodyRaw`.

El campo `RequestHeaders` contiene los headers que fueron enviados en el request (incluyendo los que provienen de variables de entorno resueltas).

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

Soporta archivos `.pfx` (PKCS#12). Si solo se necesita un certificado sin contrasena, se puede omitir `password`.

---

## Reintentos (retry)

Cuando un request falla por `HttpRequestException`, `TaskCanceledException` o `OperationCanceledException`, APITester puede reintentar automaticamente. Las cancelaciones externas (por ejemplo, Ctrl+C) no se reintentan.

```json
{
  "retries": 3,
  "retryDelayMs": 2000
}
```

Esto reintenta hasta 3 veces con 2 segundos de espera entre cada intento. Si todos los intentos fallan, se devuelve el error del ultimo intento.

---

## Arquitectura

```
APITester.slnx
├── APITester.Core           ← Logica compartida (modelos, interfaces, utilidades)
│   ├── Models/              ← ApiResponse, CertConfig, CliArgs, ExecutionSummary
│   └── Services/            ← ConfigValidator, ConsolePresenter, EnvVarResolver,
│                               GenericConfigLoader, JsonFormatter, RetryPolicy, etc.
├── APITester.Rest           ← Implementacion REST
│   ├── Program.cs           ← Punto de entrada
│   ├── Models/              ← RestRequestConfig
│   └── Services/            ← HttpExecutor, RestConfigLoader
└── APITester.Tests          ← Tests unitarios
```

El diseno separa el nucleo (`Core`) del protocolo especifico (`Rest`), lo que permite agregar otros protocolos (GraphQL, gRPC, etc.) implementando `IApiExecutor<T>`.

---

## Ejecucion paralela

Los requests se ejecutan en paralelo con un limite de concurrencia de hasta 4 simultaneos. Esto reduce el tiempo total cuando se procesan multiples endpoints independientes. El orden de los resultados en el archivo JSON de salida se preserva segun el orden definido en el archivo de configuracion.

---

## Ejemplos

### GET con query params y verbose

```bash
dotnet run -- -c config.json -v
```

### POST con body y timeout personalizado

```json
{
  "name": "Crear recurso",
  "url": "https://api.ejemplo.com/posts",
  "method": "POST",
  "headers": {
    "Content-Type": "application/json"
  },
  "body": "{\"title\": \"Ejemplo\", \"body\": \"Contenido de prueba\", \"userId\": 1}",
  "timeout": 15,
  "output": "crear-post.json"
}
```

### Multiples requests

```json
[
  {
    "name": "Health check",
    "url": "https://api.ejemplo.com/health",
    "method": "GET",
    "output": "health.json"
  },
  {
    "name": "Listar productos",
    "url": "https://api.ejemplo.com/productos",
    "method": "GET",
    "headers": {
      "Authorization": "Bearer ${TOKEN}"
    },
    "output": "productos.json"
  }
]
```
