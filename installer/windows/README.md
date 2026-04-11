# Windows MSI Installer (WiX Toolset)

## Prerequisites
- [WiX Toolset v4+](https://wixtoolset.org/docs/intro/)
- .NET 10 SDK
- Windows OS (WiX only runs on Windows)

## Build Steps

1. Publish the application:
   ```powershell
   dotnet publish Dari.App/Dari.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish/win-x64
   ```

2. Build the MSI:
   ```powershell
   dotnet tool install --global wix
   wix build installer/windows/Product.wxs -d PublishDir=publish/win-x64 -o Dari-1.0.0-x64.msi
   ```

## What the installer does
- Installs Dari to `%LOCALAPPDATA%\Dari\app`
- Creates Start Menu shortcut
- Registers `.dar` file association with `application/x-dari-archive` MIME type
- Adds Explorer context menu entries:
  - "Open with Dari"
  - "Extract here (Dari)"
- Supports per-user installation (no admin required)
- Major upgrade support (newer version replaces older)
