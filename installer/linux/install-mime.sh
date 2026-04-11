#!/bin/bash
# Post-install script for registering the .dar MIME type on Linux.
# Run after placing x-dari-archive.xml into /usr/share/mime/packages/
# (or ~/.local/share/mime/packages/ for per-user install).

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"

# Per-user installation
MIME_DIR="${XDG_DATA_HOME:-$HOME/.local/share}/mime"

mkdir -p "$MIME_DIR/packages"
cp "$SCRIPT_DIR/x-dari-archive.xml" "$MIME_DIR/packages/"

# Update the MIME database
if command -v update-mime-database &> /dev/null; then
    update-mime-database "$MIME_DIR"
    echo "MIME type 'application/x-dari-archive' registered for .dar files."
else
    echo "Warning: update-mime-database not found. MIME type registered but cache not updated."
    echo "Install shared-mime-info package and run: update-mime-database $MIME_DIR"
fi

# Install desktop file
DESKTOP_DIR="${XDG_DATA_HOME:-$HOME/.local/share}/applications"
mkdir -p "$DESKTOP_DIR"
cp "$SCRIPT_DIR/../../Dari.App/platform/dari.desktop" "$DESKTOP_DIR/" 2>/dev/null || true

if command -v update-desktop-database &> /dev/null; then
    update-desktop-database "$DESKTOP_DIR"
fi

echo "Done. .dar files are now associated with Dari."
