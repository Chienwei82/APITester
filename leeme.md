# APITester

Herramienta portable de linea de comandos para ejecutar requests HTTP contra APIs REST y guardar las respuestas en un archivo JSON.

## Uso

```bash
./APITester.Rest -c rest-config.json
```

### Argumentos

| Argumento | Descripcion |
|---|---|
| `-c`, `--config` | Ruta al archivo JSON con la configuracion de requests |
| `-o`, `--output` | Archivo de salida (opcional) |
| `-v`, `--verbose` | Muestra detalles adicionales |
| `-h`, `--help` | Muestra la ayuda |

## Archivo de configuracion

El archivo `rest-config.json` incluido es un ejemplo funcional que consulta JSONPlaceholder.

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

### Campos disponibles

| Campo | Tipo | Obligatorio | Defecto |
|---|---|---|---|
| `name` | string | no | `"{method} {url}"` |
| `url` | string | **si** | — |
| `method` | string | no | `GET` |
| `headers` | mapa | no | — |
| `query` | mapa | no | — |
| `body` | string | no | — |
| `output` | string | no | `rest-response.json` |
| `timeout` | int | no | `30` |
| `retries` | int | no | `0` |
| `retryDelayMs` | int | no | `1000` |
| `cert.path` | string | no | — |
| `cert.password` | string | no | — |

## Variables de entorno

Se puede usar `${NOMBRE_VAR}` en cualquier campo y se reemplaza con el valor de la variable de entorno.

## Certificados TLS

Para autenticacion mutua TLS (mTLS):

```json
{
  "cert": {
    "path": "/ruta/cliente.pfx",
    "password": "${CERT_PASSWORD}"
  }
}
```

## Reintentos

```json
{
  "retries": 3,
  "retryDelayMs": 2000
}
```

Los requests se ejecutan en paralelo con un limite de 4 simultaneos.

Repositorio: https://github.com/Chienwei82/APITester
