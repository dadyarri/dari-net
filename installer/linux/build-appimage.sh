#!/bin/bash
# Build an AppImage for Dari
# Prerequisites: appimagetool (https://github.com/AppImage/appimagetool)
# Usage: ./build-appimage.sh [publish-dir]

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
PUBLISH_DIR="${1:-$REPO_ROOT/publish/linux-x64}"
APPDIR="$REPO_ROOT/build/Dari.AppDir"
VERSION="${DARI_VERSION:-1.0.0}"

# Clean
rm -rf "$APPDIR"
mkdir -p "$APPDIR/usr/bin"
mkdir -p "$APPDIR/usr/share/applications"
mkdir -p "$APPDIR/usr/share/mime/packages"
mkdir -p "$APPDIR/usr/share/icons/hicolor/256x256/apps"
mkdir -p "$APPDIR/usr/share/metainfo"

# Copy published files
cp -r "$PUBLISH_DIR"/* "$APPDIR/usr/bin/"

# Desktop file
cp "$REPO_ROOT/Dari.App/platform/dari.desktop" "$APPDIR/usr/share/applications/"
cp "$REPO_ROOT/Dari.App/platform/dari.desktop" "$APPDIR/"

# MIME type definition
cp "$SCRIPT_DIR/x-dari-archive.xml" "$APPDIR/usr/share/mime/packages/"

# AppStream metadata
if [ -f "$SCRIPT_DIR/net.dadyarri.dari.metainfo.xml" ]; then
    cp "$SCRIPT_DIR/net.dadyarri.dari.metainfo.xml" "$APPDIR/usr/share/metainfo/"
fi

# Icon
if [ -f "$REPO_ROOT/Dari.App/Assets/dari.png" ]; then
    cp "$REPO_ROOT/Dari.App/Assets/dari.png" "$APPDIR/usr/share/icons/hicolor/256x256/apps/dari.png"
    cp "$REPO_ROOT/Dari.App/Assets/dari.png" "$APPDIR/dari.png"
fi

# AppRun
cat > "$APPDIR/AppRun" << 'APPRUN'
#!/bin/bash
SELF_DIR="$(dirname "$(readlink -f "$0")")"
export PATH="$SELF_DIR/usr/bin:$PATH"
exec "$SELF_DIR/usr/bin/Dari.App" "$@"
APPRUN
chmod +x "$APPDIR/AppRun"

# Build AppImage
if command -v appimagetool &> /dev/null; then
    ARCH=x86_64 appimagetool "$APPDIR" "$REPO_ROOT/build/Dari-${VERSION}-x86_64.AppImage"
    echo "AppImage created: $REPO_ROOT/build/Dari-${VERSION}-x86_64.AppImage"
else
    echo "appimagetool not found. AppDir prepared at: $APPDIR"
    echo "Install appimagetool from https://github.com/AppImage/appimagetool"
    echo "Then run: ARCH=x86_64 appimagetool $APPDIR $REPO_ROOT/build/Dari-${VERSION}-x86_64.AppImage"
fi
