#!/usr/bin/env python3
"""Empaqueta un directorio como .zip conservando el nombre de la carpeta raiz.

Uso:
    make-zip.py <directorio> <salida.zip>

Se usa en CI en lugar del binario `zip` para no depender de paquetes del
runner; solo requiere Python 3.7+ (disponible en ubuntu-latest).
"""

import os
import sys
import zipfile


def main() -> int:
    if len(sys.argv) != 3:
        print("uso: make-zip.py <directorio> <salida.zip>", file=sys.stderr)
        return 64

    source = os.path.abspath(sys.argv[1])
    output = os.path.abspath(sys.argv[2])

    if not os.path.isdir(source):
        print(f"error: no existe el directorio {source}", file=sys.stderr)
        return 1

    folder = os.path.basename(source.rstrip(os.sep))
    output_dir = os.path.dirname(output)
    if output_dir:
        os.makedirs(output_dir, exist_ok=True)

    with zipfile.ZipFile(output, "w", zipfile.ZIP_DEFLATED, compresslevel=9) as zf:
        for current, dirs, files in os.walk(source):
            dirs.sort()
            for name in sorted(files):
                path = os.path.join(current, name)
                relative = os.path.relpath(path, source)
                arcname = os.path.join(folder, relative).replace(os.sep, "/")
                zf.write(path, arcname=arcname)
                print(f"  {arcname} ({os.path.getsize(path)} bytes)")

    print(f"zip generado: {output} ({os.path.getsize(output)} bytes)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
