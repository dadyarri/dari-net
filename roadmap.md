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

### Completed app phases ✅

- Phase A — Project setup
- Phase B — Archive browser
- Phase C — Extraction
- Phase D — Archive creation
- Phase E — Archive appending
- Phase F — File preview
- Phase G — Platform integration and polish

---

### Phase 1 — v6-readiness refactor

- Library should support both versions. Read version from header (header format stays same) and keep current behavior for v5; throw `NotImplementedException` for v6 for now.
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
Completed: A → B → C → D → E → F → G
Next: Phase 1 → Prepare library and app for multi-version archives (v5 + upcoming v6)
```

Each phase is independently compilable and testable before moving on to the next.
