# Releasing Dari

## 1) Pre-release checks

Run from repository root:

```bash
dotnet build
dotnet test
dotnet format
```

## 2) Versioning model

This repository keeps GitVersion configuration in `GitVersion.yml`:

- `mode: ContinuousDeployment`
- `tag-prefix: v`
- `next-version: 1.0.0`
- `main` branch → patch increments
- `feature/*` and `copilot/*` branches → prerelease tag `alpha`

### How to compute versions locally with GitVersion

Option A (global tool):

```bash
dotnet tool install --global GitVersion.Tool
dotnet-gitversion
```

Option B (without global install):

```bash
dotnet tool install --tool-path /tmp/dari-tools GitVersion.Tool
/tmp/dari-tools/dotnet-gitversion
```

Useful outputs:

- `SemVer` (for package/release version)
- `AssemblySemFileVer`
- `InformationalVersion`

## 3) What CI release workflow does

Release workflow: `.github/workflows/release.yml`

- Triggers on:
  - tags `v*`
  - branches `feature/**` and `copilot/**` (prerelease flow)
- Publishes self-contained single-file app for:
  - `win-x64`
  - `linux-x64`
- Builds:
  - Windows Inno Setup installer (`dari-win-x64-installer.exe`)
  - Linux AppImage bundle (`dari-linux-x64-appimage.tar.gz`)
- Creates GitHub Release with generated notes and uploads artifacts

Version value used in workflow:

- tag push `vX.Y.Z` → `X.Y.Z`
- branch push → `<Dari.App.csproj Version>-<branch-slug>.<short-sha>`

Informational version includes archive format suffix:

- `<version>+<formatVersion>`

where `formatVersion` is read from `Dari.Archiver/Format/DariConstants.cs`.

## 4) How to cut release

1. Merge release-ready changes to `main`.
2. Create tag:

   ```bash
   git tag vX.Y.Z
   git push origin vX.Y.Z
   ```

3. Wait for `Release` workflow completion.
4. Verify GitHub Release assets:
   - `dari-win-x64-installer.exe`
   - `dari-linux-x64-appimage.tar.gz`
5. Smoke test both artifacts before announcement.

## 5) Local packaging quick checks

Windows installer:

- see `installer/windows/README.md`

Linux AppImage:

- see `installer/linux/README.md`
