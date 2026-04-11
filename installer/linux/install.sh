#!/usr/bin/env bash
# Install the Dari AppImage and register system integration.
# Run from the directory that contains dari-*-x86_64.AppImage.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

APPIMAGE="$(find "$SCRIPT_DIR" -maxdepth 1 -name 'Dari-*-x86_64.AppImage' | head -1)"
if [[ -z "${APPIMAGE}" ]]; then
    echo "Error: no Dari AppImage found in $(dirname "$0")." >&2
    exit 1
fi

# Choose install prefix: system-wide when run as root, per-user otherwise
if [[ "${EUID:-$(id -u)}" -eq 0 ]]; then
    BIN_DIR="/usr/local/bin"
    DESKTOP_DIR="/usr/share/applications"
    METAINFO_DIR="/usr/share/metainfo"
    MIME_DIR="/usr/share/mime"
else
    BIN_DIR="${HOME}/.local/bin"
    DESKTOP_DIR="${HOME}/.local/share/applications"
    METAINFO_DIR="${HOME}/.local/share/metainfo"
    MIME_DIR="${XDG_DATA_HOME:-${HOME}/.local/share}/mime"
fi

mkdir -p "${BIN_DIR}" "${DESKTOP_DIR}" "${METAINFO_DIR}" "${MIME_DIR}/packages"

# Install AppImage as 'dari'
install -m 755 "${APPIMAGE}" "${BIN_DIR}/dari"

# Register MIME type for .dar files
if [[ -f "${SCRIPT_DIR}/x-dari-archive.xml" ]]; then
    install -m 644 "${SCRIPT_DIR}/x-dari-archive.xml" "${MIME_DIR}/packages/x-dari-archive.xml"
    if command -v update-mime-database &>/dev/null; then
        update-mime-database "${MIME_DIR}"
    fi
fi

# Install AppStream metadata
if [[ -f "${SCRIPT_DIR}/net.dadyarri.dari.metainfo.xml" ]]; then
    install -m 644 "${SCRIPT_DIR}/net.dadyarri.dari.metainfo.xml" "${METAINFO_DIR}/net.dadyarri.dari.metainfo.xml"
fi

# Install .desktop entry pointing to the installed binary
cat > "${DESKTOP_DIR}/dari.desktop" << EOF
[Desktop Entry]
Type=Application
Name=Dari
Comment=Cross-platform archive manager for .dar archives
Exec=${BIN_DIR}/dari %f
Icon=dari
Terminal=false
Categories=Utility;Archiving;
MimeType=application/x-dari-archive;
StartupWMClass=Dari
Keywords=archive;dar;compress;extract;
EOF

update-desktop-database "${DESKTOP_DIR}" 2>/dev/null || true

echo "Dari installed to ${BIN_DIR}/dari"
echo "Run 'dari' to start the application."
