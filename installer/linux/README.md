# Linux Packaging

## AppImage

### Prerequisites
- .NET 10 SDK
- [appimagetool](https://github.com/AppImage/appimagetool)

### Build Steps

1. Publish the application:
   ```bash
   dotnet publish Dari.App/Dari.App.csproj -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -o publish/linux-x64
   ```

2. Build the AppImage:
   ```bash
   DARI_VERSION=1.0.0 ./installer/linux/build-appimage.sh publish/linux-x64
   ```

The resulting AppImage will be at `build/Dari-1.0.0-x86_64.AppImage`.

## MIME Type Registration

To register `.dar` file association with the system:

```bash
./installer/linux/install-mime.sh
```

This installs:
- MIME type `application/x-dari-archive` for `.dar` files (with magic bytes detection)
- `.desktop` file for desktop environment integration
- Updates MIME and desktop databases via `update-mime-database` and `update-desktop-database`

## Files
- `x-dari-archive.xml` — Freedesktop shared-mime-info definition for `.dar` files
- `build-appimage.sh` — Script to assemble and build the AppImage
- `install-mime.sh` — Script to register MIME type for per-user installation
- `../../Dari.App/platform/dari.desktop` — Desktop entry file
