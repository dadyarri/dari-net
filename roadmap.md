# Dari — Roadmap

---

## Part 1 — `Dari.Archiver` (library) ✅ Complete

> **Target:** .NET 10 / C# 13 class library  
> **Format:** Dari v5 (see `docs/src/archive_structure.md`)  
> **Goal:** A correct, high-performance, fully-managed reader/writer for `.dar` archives.

### Project structure

```
Dari.Archiver/
├── Format/            # Dari v5 format primitives
│   ├── DariConstants.cs         # Magic bytes, version, fixed sizes
│   ├── DariHeader.cs            # 13-byte header
│   ├── DariFooter.cs            # 15-byte footer
│   ├── IndexEntry.cs            # 85-byte fixed part + path + extra
│   ├── IndexFlags.cs            # [Flags] LinkedData=0x0001, EncryptedData=0x0002
│   ├── CompressionMethod.cs     # None=0, Brotli=1, Zstandard=2, Lzma=3, LeptonJpeg=4
│   ├── Blake3Hash.cs            # 32-byte value type with IEquatable<T>
│   └── FileMetadata.cs          # mtime, uid, gid, perm
│
├── IO/                # Low-level binary I/O
│   ├── DariReader.cs            # Stream → header / footer / index / data blocks
│   ├── DariWriter.cs            # Writes header, data blocks, index, footer
│   └── BinaryHelpers.cs         # Span wrappers over BinaryPrimitives
│
├── Compression/       # Compression pipeline
│   ├── ICompressor.cs           # CompressAsync / DecompressAsync
│   ├── CompressorRegistry.cs    # CompressionMethod → ICompressor; ext → method
│   ├── NoneCompressor.cs
│   ├── BrotliCompressor.cs      # quality=6, lgwin=22
│   ├── ZstandardCompressor.cs   # ZstdSharp, level=3
│   └── LzmaCompressor.cs        # SharpCompress XZ, preset=9
│
├── Crypto/            # ChaCha20-Poly1305 encryption
│   ├── DariEncryption.cs        # KDF, Encrypt, Decrypt
│   └── DariPassphrase.cs        # Value-object; ZeroMemory on Dispose
│
├── Deduplication/
│   └── DeduplicationTracker.cs  # checksum → (offset, method); LinkedData entries
│
├── Extra/
│   ├── ExtraField.cs            # Parse / serialize "k=v;k=v"
│   └── WellKnownExtraKeys.cs    # "e", "en", "et", "imk", "imd", "idt" …
│
├── Ignoring/          # .darignore / .gitignore filtering
│   ├── IIgnoreFilter.cs
│   └── GitIgnoreFilter.cs       # Hierarchical loading via the Ignore package
│
├── Archiving/         # High-level API
│   ├── ArchiveReader.cs         # Open, enumerate, extract
│   ├── ArchiveWriter.cs         # Create archive, add files / directories
│   └── ArchiveAppender.cs       # Atomic append to an existing archive
│
└── Diagnostics/
    └── DariFormatException.cs   # Thrown on Dari v5 format violations
```

### Dependencies

| Package | Version | Purpose |
|---------|---------|---------|
| `Blake3` | 2.2.1 | BLAKE3 checksums and KDF |
| `ZstdSharp.Port` | 0.8.7 | Zstandard compression (pure managed) |
| `SharpCompress` | 0.47.3 | LZMA/XZ compression |
| `Ignore` | 0.2.1 | `.gitignore` rule parsing (spec 2.29.2) |

### Completed phases

| Phase | Description | Status |
|-------|-------------|--------|
| 1 | Format primitives — `DariConstants`, `DariHeader`, `DariFooter`, `IndexEntry` | ✅ |
| 2 | Low-level reader `DariReader` | ✅ |
| 3 | Low-level writer `DariWriter` | ✅ |
| 4 | Compression — `ICompressor`, `CompressorRegistry`, Brotli / Zstd / LZMA / None | ✅ |
| 5 | Encryption — `DariEncryption`, `DariPassphrase` | ✅ |
| 6 | Extra fields — `ExtraField`, `WellKnownExtraKeys` | ✅ |
| 7 | High-level API — `ArchiveReader`, `ArchiveWriter` | ✅ |
| 8 | Deduplication — `DeduplicationTracker`, `LinkedData` entries | ✅ |
| 9 | Archive appending — `ArchiveAppender` (atomic rename-swap) | ✅ |
| 10 | Tests — 159 xUnit tests (all passing) | ✅ |

### Notes

- `ArchiveWriter.AddDirectoryAsync` walks the tree recursively and applies a `GitIgnoreFilter`
  that loads `.darignore` / `.gitignore` hierarchically from each directory.
  Verified against the reference archive — entry count matches exactly (561 entries).
- `DariWriter.DisposeAsync` auto-finalizes on best-effort so `await using` works without
  an explicit `FinalizeAsync()` call.
- Linked (deduplicated) entries inherit the primary entry's `CompressionMethod` so they
  decompress correctly through `ArchiveReader.ExtractAsync`.

---

## Part 2 — `Dari.App` (GUI application)

> **Goal:** Cross-platform desktop GUI (.NET 10, Avalonia 11) for working with `.dar` archives  
> on Windows, macOS, and Linux from a single codebase.

### Goals

| Goal | Description |
|------|-------------|
| **Cross-platform** | Windows 10+, macOS 12+, Linux (X11/Wayland) — one codebase |
| **Modern UI** | Avalonia 11 + Fluent theme; native title bar per platform |
| **MVVM** | CommunityToolkit.Mvvm; no logic in code-behind |
| **Responsiveness** | All I/O is async; progress and cancellation via `CancellationToken` |
| **Security** | Passphrase lives only in `DariPassphrase` (ZeroMemory on close) |

### Project structure

```
Dari.App/
├── Assets/                  # Icons, fonts, resources
├── Controls/                # Reusable Avalonia controls
│   ├── FileIconControl.axaml       # Icon resolved by file extension
│   └── SizeLabel.axaml             # Formatted size display (KB / MB / GB)
├── Converters/              # IValueConverter for bindings
├── Models/                  # UI-layer domain models
│   └── ArchiveEntryViewModel.cs
├── Services/                # Service abstractions (for testability)
│   ├── IDialogService.cs           # File dialogs, notifications
│   ├── IClipboardService.cs
│   └── IProgressService.cs
├── ViewModels/
│   ├── MainWindowViewModel.cs      # Shell: menu, tabs, status bar
│   ├── ArchiveBrowserViewModel.cs  # Browse entries, filter, sort
│   ├── CreateArchiveViewModel.cs   # New-archive wizard
│   ├── ExtractViewModel.cs         # Extraction progress
│   ├── PasswordPromptViewModel.cs  # Passphrase entry for encrypted archives
│   └── PreviewViewModel.cs         # In-pane file content preview
└── Views/
    ├── MainWindow.axaml
    ├── ArchiveBrowserView.axaml
    ├── CreateArchiveView.axaml
    ├── ExtractView.axaml
    ├── PasswordPromptView.axaml
    └── PreviewView.axaml
```

### Dependencies ✅

| Package | Purpose |
|---------|---------|
| `Avalonia` 11.x | UI framework |
| `Avalonia.Themes.Fluent` | Fluent design theme |
| `Avalonia.Desktop` | Native file dialogs |
| `CommunityToolkit.Mvvm` | `[ObservableProperty]`, `[RelayCommand]`, `WeakReferenceMessenger` |
| `Dari.Archiver` | Project reference to the archiver library |

---

### Phase A — Project setup ✅

**Goal:** Create the solution skeleton; app launches and runs on all three platforms.

- Add `Dari.App` as an Avalonia Application project to `Dari.slnx`
- Target `net10.0`, `Nullable enable`, `LangVersion preview`
- Reference `Avalonia.Themes.Fluent`, `CommunityToolkit.Mvvm`, `Dari.Archiver`
- Implement `MainWindow` with a basic Fluent theme and empty content area
- Configure `app.manifest` (Windows), `Info.plist` (macOS), `.desktop` file (Linux)
- Add application icon in `.ico` / `.icns` / `.png` formats
- Verify build and launch on Windows, macOS, Linux

---

### Phase B — Archive browser ✅

**Goal:** Open a `.dar` file and display its entries.

**Key components:**

- `MainWindowViewModel` — `OpenArchiveCommand`, `CloseArchiveCommand`; opens a system file dialog via `IDialogService`
- `ArchiveBrowserViewModel` — holds `IReadOnlyList<ArchiveEntryViewModel>`; supports flat list and directory tree modes; sorts by name / size / date / compression ratio
- `ArchiveBrowserView` — `DataGrid` with columns: name, path, size, compressed size, ratio, algorithm, date, permissions
- `ArchiveEntryViewModel` — wraps `IndexEntry`; computed properties `CompressionRatio`, `IsEncrypted`, `IsLinked`, icon by extension
- Archive header metadata: creation date, file count, total / compressed size
- Search bar with real-time filtering bound to `SearchText`
- Drag & drop a `.dar` file onto the window to open it
- If the archive is encrypted, show `PasswordPromptView` before reading entries

---

### Phase C — Extraction ✅

**Goal:** Extract selected entries or the entire archive to disk.

**Key components:**

- `ExtractViewModel` — list of selected entries or "all"; destination path; `Progress<double>`; `CancellationTokenSource`
- Commands on `ArchiveBrowserViewModel`:
  - `ExtractSelectedCommand` — extract checked entries
  - `ExtractAllCommand` — extract whole archive
  - `OpenInExplorerCommand` — reveal destination directory after extraction
- `ExtractView` — modal dialog with progress bar, file counter, and Cancel button
- Name conflicts: dialog offering Overwrite / Skip / Rename
- Checksum errors: separate notification with option to continue despite errors
- Completion summary notification with count of extracted files

---

### Phase D — Archive creation ✅

**Goal:** Create a new `.dar` archive from selected files or a directory.

**Key components:**

- `CreateArchiveViewModel` — three-step wizard:
  1. **Source** — pick a directory or individual files; preview the file tree respecting `.darignore` / `.gitignore`
  2. **Options** — compression algorithm (Brotli / Zstd / LZMA / Auto / None); enable deduplication checkbox; encryption (password + confirmation)
  3. **Destination** — output `.dar` path; Create button
- Creation progress via `ArchiveWriter.AddDirectoryAsync` + `IProgress<(int done, int total, string currentFile)>`
- After creation, open the new archive in the browser
- Drag & drop a folder onto an empty window to start the creation wizard

---

### Phase E — Archive appending

**Goal:** Add files to an already-open archive.

**Key components:**

- `AppendFilesCommand` on `MainWindowViewModel`
- Drag & drop files / folders onto an open `ArchiveBrowserView`
- File picker dialog for selecting files to add
- Uses `ArchiveAppender.OpenAsync` under the hood
- Shows progress and refreshes `ArchiveBrowserViewModel` on success
- If the archive is encrypted, prompts for the passphrase before appending, that must match passphrase already used in the archive. Show error to user after confirmation in separate popup and ask for passphrase again if not.
- Success notification with count of added files and deduplicated blocks

---

### Phase F — File preview

**Goal:** Show entry content in-pane without full extraction.

**Key components:**

- `PreviewViewModel` — reads the raw block via `ArchiveReader.OpenRawBlockAsync`, decodes on the fly; capped at 1 MB
- Preview types:
  - **Text** — UTF-8 / Latin-1 / Windows-1251 for text; display in plain `TextBlock`
  - **Code** - UTF-8 / Latin-1 / Windows-1251. Should have syntax highlight by common extensions
  - **Images** — `Bitmap` via Avalonia (`png`, `jpg`, `bmp`, `gif`, `webp`)
  - **Other** — hex dump of the first 512 bytes
- Preview pane to the right of the entry list; updates on selection change (250 ms debounce)
- "Extract & Open" button — extracts to a temp folder and opens with the system default app

---

### Phase G — Platform integration and polish

**Goal:** Native feel on every platform; final UX refinement.

**Windows:**
- Generate `.msi` installer with WiX Toolset (without code signing)
- Register `.dar` file association on installation
- Explorer context menu: "Open with Dari", "Extract here"
- Add to installer creating associations with `application/x-dari-archive` MIME type for `.dar` files

**Linux:**
- `.desktop` file with MIME type `application/x-dari-archive`
- XDG `MimeInfo.cache` updated by installer via `update-mime-database`
- Wayland and X11 support via Avalonia
- Pack AppImage file

**General:**
- Light / dark theme following the system `ActualThemeVariant`
- Keyboard shortcuts: `Ctrl+O` (open), `Ctrl+E` (extract), `Ctrl+N` (new), `Ctrl+W` (close)
- Recent files list in menu (stored in `%APPDATA%` / `~/.config/dari/recent.json`) with check if files are existing and button to remove item from recent
- Settings: default extraction directory, theme, language, separate buttons `Apply` (save settings without closing the window) and `Ok` (Save settings and close the window)
- About -> About Dari should open popup with information of the app (stop here and ask, what information needs to be put there with some examples of what is usually resides there)
- Introduce some tool to publish releases in SemVer notation (i. e. GitVersion?) and github workflow actions to publish releases with binaries for windows and linux on tags push.

---

### Phase H - Refactor and preparation to support next version of the archive (with preservation of old logic at the same time)

- Library should support both versions. Currently read the version from the header of the archive (stays the same format) and work as usual for v5, but throw NotImplementedException for v6
- App should handle that exception and show message that version 6 is currently unsupported
- Migrate app to Avalonia 12: https://docs.avaloniaui.net/docs/avalonia12-breaking-changes

---

### Application tests

| Test class | Coverage |
|------------|----------|
| `ArchiveBrowserViewModelTests` | Open archive, filter, sort |
| `ExtractViewModelTests` | Progress, cancellation, name conflicts |
| `CreateArchiveViewModelTests` | Creation options, path validation |
| `PasswordPromptViewModelTests` | Correct / wrong passphrase |
| `AppendViewModelTests` | Append files, deduplication |
| `IntegrationTests` | Create → open → extract (headless Avalonia) |

---

### Implementation order

```
Phase A  →  Project setup (skeleton, theme, launch on all platforms)
Phase B  →  Archive browser (open file, DataGrid, search)
Phase C  →  Extraction (selected entries and full archive)
Phase D  →  Archive creation (wizard, progress, ignore-filter preview)
Phase E  →  Archive appending (ArchiveAppender, drag & drop)
Phase F  →  File preview (text, images, hex dump)
Phase G  →  Platform integration (file associations, native menus, notarization)
Phase H  →  Prepare lib and gui app to support multple archive versions (current is 5, next is 6, currently is in progress)
```

Each phase is independently compilable and testable before moving on to the next.
