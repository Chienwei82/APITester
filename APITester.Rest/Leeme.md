# API Tester REST

Cliente REST portable para probar APIs HTTP (GET, POST, PUT, PATCH, DELETE).

.NET 10 | YAML | portable

---

## Requisitos

- .NET SDK 10.0+ (`dotnet --version`)

## Ejecutar

```bash
cd ~/mycode/APITester.Rest

dotnet run                          # usa rest-config.yaml
dotnet run -- -v                    # modo verbose
dotnet run -- -c mi-config.yaml     # configuracion personalizada
dotnet run -- -o salida.json        # archivo de salida
dotnet run -- -h                    # ayuda
```

## Compilar ejecutable portable

No requiere .NET SDK en la maquina destino. El ejecutable pesa ~70MB.

### Linux (x64)

```bash
dotnet publish -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true
# -> bin/Release/net10.0/linux-x64/publish/APITester.Rest
```

### Windows (x64)

```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
# -> bin\Release\net10.0\win-x64\publish\APITester.Rest.exe
```

### macOS (x64)

```bash
dotnet publish -c Release -r osx-x64 --self-contained true -p:PublishSingleFile=true
# -> bin/Release/net10.0/osx-x64/publish/APITester.Rest
```

## Formato YAML (`rest-config.yaml`)

```yaml
# Un solo request
---
name: "Obtener usuarios"
url: "https://api.ejemplo.com/usuarios"
method: GET                     # GET | POST | PUT | PATCH | DELETE
headers:                        # opcional
  Authorization: "Bearer token"
  X-Custom: "valor"
query:                          # opcional (query string params)
  page: "1"
  limit: "10"
body: |                         # opcional (POST/PUT/PATCH)
  { "nombre": "Juan", "email": "juan@test.com" }
cert:                           # opcional (certificado cliente)
  path: "/ruta/certificado.pfx"
  password: "MiPassword"
output: "respuesta.json"        # opcional (default: rest-response.json)
timeout: 30                     # opcional (default: 30s)

# Multiples requests (se ejecutan en orden)
# ---
# - url: "https://api1.com/items"
#   method: GET
# - url: "https://api1.com/items"
#   method: POST
#   body: '{"nombre": "test"}'
```

## Ejemplo rapido

```bash
cat > /tmp/prueba.yaml << 'EOF'
name: "Test GET"
url: "https://jsonplaceholder.typicode.com/posts/1"
method: GET
output: "/tmp/test.json"
EOF

cd ~/mycode/APITester.Rest
dotnet run -- -c /tmp/prueba.yaml -v
```

## Salida JSON

```json
{
  "request": {
    "name": "Obtener usuarios",
    "url": "https://api.ejemplo.com/usuarios",
    "method": "GET"
  },
  "response": {
    "statusCode": 200,
    "statusText": "OK",
    "headers": { "Content-Type": "application/json" },
    "body": { ... },
    "bodyRaw": "...",
    "timeMs": 342,
    "sizeBytes": 1234
  },
  "error": null
}
```

## Argumentos CLI

| Argumento | Descripcion | Default |
|-----------|-------------|---------|
| `-c, --config` | Ruta al YAML | `rest-config.yaml` |
| `-o, --output` | Archivo JSON de salida | el del YAML o `rest-response.json` |
| `-v, --verbose` | Muestra query, body, certificado | false |
| `-h, --help` | Ayuda completa | - |

---

*Creado con Hermes Agent | .NET 10*
