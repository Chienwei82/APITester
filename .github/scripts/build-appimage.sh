#!/usr/bin/env bash
#
# Empaqueta un publish self-contained de Linux x64 como AppImage.
#
# Uso:
#   build-appimage.sh <directorio-publish> <archivo.AppImage>
#
# El directorio de publish debe contener el ejecutable `APITester.Rest`
# (salida de `dotnet publish -r linux-x64 --self-contained true
# -p:PublishSingleFile=true`).
#
# Variables de entorno opcionales:
#   APPIMAGETOOL      appimagetool ya descargado (si no, se descarga)
#   APPIMAGETOOL_URL  URL de descarga (por defecto el build continuous x86_64)
#   APPIMAGE_ICON     PNG 512x512 para el icono (por defecto assets/apitester.png)
#   APPIMAGE_ARCH     arquitectura del AppImage (por defecto x86_64)
#   APPIMAGE_BINARY   nombre del ejecutable dentro del AppDir (por defecto APITester)

set -euo pipefail

if [ "$#" -ne 2 ]; then
    echo "uso: $0 <directorio-publish> <archivo.AppImage>" >&2
    exit 64
fi

PUBLISH_DIR="$(cd "$1" && pwd)"
OUTPUT="$2"
case "$OUTPUT" in
    /*) ;;
    *) OUTPUT="$(pwd)/$OUTPUT" ;;
esac

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"

ARCH="${APPIMAGE_ARCH:-x86_64}"
BINARY_NAME="${APPIMAGE_BINARY:-APITester}"
ICON="${APPIMAGE_ICON:-$REPO_ROOT/assets/apitester.png}"
APPIMAGETOOL_URL="${APPIMAGETOOL_URL:-https://github.com/AppImage/appimagetool/releases/download/continuous/appimagetool-${ARCH}.AppImage}"

if [ ! -x "$PUBLISH_DIR/APITester.Rest" ]; then
    echo "error: no se encontro el ejecutable $PUBLISH_DIR/APITester.Rest" >&2
    echo "       ejecuta antes: dotnet publish APITester.Rest/APITester.Rest.csproj -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -o <dir>" >&2
    exit 1
fi

if [ ! -f "$ICON" ]; then
    echo "error: no se encontro el icono $ICON" >&2
    exit 1
fi

WORK_DIR="$(mktemp -d)"
trap 'rm -rf "$WORK_DIR"' EXIT

APPDIR="$WORK_DIR/AppDir"
mkdir -p \
    "$APPDIR/usr/bin" \
    "$APPDIR/usr/share/applications" \
    "$APPDIR/usr/share/icons/hicolor/512x512/apps" \
    "$APPDIR/usr/share/doc/apitester"

install -m 0755 "$PUBLISH_DIR/APITester.Rest" "$APPDIR/usr/bin/$BINARY_NAME"
install -m 0644 "$ICON" "$APPDIR/apitester.png"
install -m 0644 "$ICON" "$APPDIR/usr/share/icons/hicolor/512x512/apps/apitester.png"
ln -s apitester.png "$APPDIR/.DirIcon"

for doc in README.md LICENSE; do
    if [ -f "$REPO_ROOT/$doc" ]; then
        install -m 0644 "$REPO_ROOT/$doc" "$APPDIR/usr/share/doc/apitester/$doc"
    fi
done

cat > "$APPDIR/apitester.desktop" <<'DESKTOP'
[Desktop Entry]
Type=Application
Name=API Tester
GenericName=Cliente REST
Comment=Ejecuta requests HTTP contra APIs REST y guarda las respuestas
Exec=APITester
Icon=apitester
Terminal=true
Categories=Development;Utility;
Keywords=api;rest;http;testing;cli;
DESKTOP
install -m 0644 "$APPDIR/apitester.desktop" "$APPDIR/usr/share/applications/apitester.desktop"

# .NET self-contained single-file extrae las librerias nativas en tiempo de
# ejecucion; el AppDir es de solo lectura, asi que se fuerza un directorio
# escribible en la cache del usuario.
cat > "$APPDIR/AppRun" <<'APPRUN'
#!/bin/sh
set -e
HERE="$(dirname "$(readlink -f "$0")")"
: "${XDG_CACHE_HOME:=$HOME/.cache}"
export DOTNET_BUNDLE_EXTRACT_BASE_DIR="${DOTNET_BUNDLE_EXTRACT_BASE_DIR:-$XDG_CACHE_HOME/apitester/dotnet}"
exec "$HERE/usr/bin/APITester" "$@"
APPRUN
chmod 0755 "$APPDIR/AppRun"

# appimagetool tambien es un AppImage: se extrae para no depender de FUSE.
APPIMAGETOOL_BIN="${APPIMAGETOOL:-}"
if [ -z "$APPIMAGETOOL_BIN" ]; then
    if command -v appimagetool >/dev/null 2>&1; then
        APPIMAGETOOL_BIN="$(command -v appimagetool)"
    else
        echo "descargando appimagetool desde $APPIMAGETOOL_URL" >&2
        curl -fsSL --retry 3 --retry-delay 2 -o "$WORK_DIR/appimagetool" "$APPIMAGETOOL_URL"
        chmod +x "$WORK_DIR/appimagetool"
        APPIMAGETOOL_BIN="$WORK_DIR/appimagetool"
    fi
fi

APPIMAGETOOL_RUN="$APPIMAGETOOL_BIN"
mkdir -p "$WORK_DIR/tool"
if (cd "$WORK_DIR/tool" && "$APPIMAGETOOL_BIN" --appimage-extract >/dev/null 2>&1); then
    APPIMAGETOOL_RUN="$WORK_DIR/tool/squashfs-root/AppRun"
fi

mkdir -p "$(dirname "$OUTPUT")"
ARCH="$ARCH" "$APPIMAGETOOL_RUN" "$APPDIR" "$OUTPUT"
chmod +x "$OUTPUT"

echo "AppImage generado: $OUTPUT"
