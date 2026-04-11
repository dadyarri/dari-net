# Windows Installer (Inno Setup)

## Prerequisites
- [Inno Setup 6](https://jrsoftware.org/isinfo.php)
- .NET 10 SDK

## Build Steps

1. Publish the application:
   ```powershell
   dotnet publish Dari.App/Dari.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish/win-x64
   ```

2. Build the installer:
   ```powershell
   & "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" `
       /DMyAppVersion="1.0.0" `
       /DMyPublishDir="$PWD\publish\win-x64" `
       installer\windows\dari.iss
   ```

   The output file `dari-win-x64-installer.exe` is placed in the repository root.

## What the installer does
- Installs Dari to `%LOCALAPPDATA%\Dari\app` (no administrator rights required)
- Creates a Start Menu shortcut
- Registers `.dar` file association (`application/x-dari-archive`)
- Adds Explorer context menu entries on `.dar` files:
  - **Open with Dari**
  - **Extract here (Dari)**
- Supports clean uninstall (removes all registry keys and files)
