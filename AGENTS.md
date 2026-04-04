# Dari — Agent Guide

## Project Overview

The repository contains two projects:

| Project | Type | Purpose |
|---|---|---|
| `Dari.Archiver` | .NET 10 class library | Read/write support for the **Dari v5** binary archive format |
| `Dari.App` | .NET 10 Avalonia desktop app | Cross-platform GUI for managing `.dar` archives |
| `Dari.Archiver.Tests` | xUnit test project | 159 tests for `Dari.Archiver` |

## Build & Test
```sh
dotnet build          # build everything (TreatWarningsAsErrors=true)
dotnet test           # run all xUnit tests
dotnet format         # run after completing any task to enforce code style
```
SDK version is pinned to `10.0.x` via `global.json`. `LangVersion=preview` is set, so all C# 13/preview features are available.

---

## Dari.Archiver — Library

All layers are complete (✅).

| Directory | Purpose |
|---|---|
| `Format/` | Dari v5 binary structs, enums, constants |
| `IO/` | `DariReader`, `DariWriter`, `BinaryHelpers` |
| `Compression/` | `ICompressor` + Brotli / Zstd / LZMA / None + `CompressorRegistry` |
| `Crypto/` | `DariEncryption`, `DariPassphrase` (ChaCha20-Poly1305, KDF via BLAKE3) |
| `Deduplication/` | `DeduplicationTracker` — content-addressed dedup via BLAKE3 checksum |
| `Archiving/` | `ArchiveReader`, `ArchiveWriter`, `ArchiveAppender` — public high-level API |
| `Extra/` | `ExtraField` key=value parser, `WellKnownExtraKeys` |
| `Ignoring/` | `.darignore` / `.gitignore` filter via `GitIgnoreFilter` |
| `Diagnostics/` | `DariFormatException` |

### Critical Format Facts (Dari v5)
- **Header**: 13 bytes at offset 0 — magic `"DARI"`, version `5`, u64 LE timestamp.
- **Footer**: last 15 bytes — magic `"DARIEND"`, u32 LE `index_offset`, u32 LE `file_count`.
- **Index entries**: fixed 85-byte struct (`IndexEntryFixed`, `Pack=1`) followed by UTF-8 path then UTF-8 extra string. Read via `MemoryMarshal.Read<IndexEntryFixed>(span)`.
- **Read order**: seek to `(fileLength - 15)` → footer → seek to 0 → header → seek to `footer.IndexOffset` → read index entries.
- Checksum is always BLAKE3 of original (uncompressed, unencrypted) bytes.
- If `compressor.output.Length >= input.Length`, store raw and set `Compression = None` (fallback rule §8.2).
- Linked (deduplicated) entries share the primary entry's `Offset`; `IndexFlags.LinkedData` is set.

### Archiver Coding Conventions
- **Format structs are `readonly struct`** with `static ReadFrom(ReadOnlySpan<byte>)` + `WriteTo(Span<byte>)` factory pattern (see `DariHeader.cs`, `DariFooter.cs`, `IndexEntry.cs`).
- **All integer I/O uses `BinaryPrimitives`** (little-endian) — never `BitConverter`.
- **`stackalloc`** for small fixed-size buffers: `stackalloc byte[DariConstants.HeaderSize]` (13 B), footer (15 B), nonce (12 B), key (32 B).
- **`ArrayPool<byte>.Shared`** for larger intermediate buffers; always return in `finally`.
- **`ValueTask`** on all async methods; `ConfigureAwait(false)` throughout.
- **`DariFormatException`** uses internal static factory methods per error kind, e.g. `DariFormatException.BadHeaderMagic()`.
- `[StructLayout(LayoutKind.Sequential, Pack=1)]` is required on any struct read via `MemoryMarshal.Read<T>`.
- `AssemblyInfo.cs` grants `InternalsVisibleTo("Dari.Archiver.Tests")` — internal types are testable directly.

### Testing Conventions
- Test classes are `public sealed class`, test methods use `[Fact]` (globally imported via `<Using Include="Xunit" />`).
- No `FluentAssertions` — use plain xUnit `Assert.*`.
- Use `stackalloc` for small byte buffers in tests (mirrors production code); use `new byte[]` for larger inputs.
- Negative tests always assert the **exception message** contains the relevant detail (e.g. `Assert.Contains("version 3", ex.Message)`).

### Key Archiver Dependencies
| Package | Use |
|---|---|
| `Blake3.NET` | BLAKE3 checksums + KDF (`"dari.v1.chacha20poly1305.key"` context) |
| `ZstdSharp.Port` | Zstandard compression (level 3) |
| `SharpCompress` | LZMA/XZ compression (preset 9, XZ container) |
| `Microsoft.IO.RecyclableMemoryStream` | Pooled `MemoryStream` to avoid LOH pressure |
| `System.Security.Cryptography.ChaCha20Poly1305` | Encryption (inbox .NET 10) |
| `Ignore` | `.gitignore` rule parsing |

### Encryption Details (§9)
Key = `blake3_derive_key("dari.v1.chacha20poly1305.key", passphrase_bytes)`.  
Nonce = first 12 bytes of the entry's BLAKE3 checksum (`Blake3Hash.CopyNonceTo`).  
Ciphertext layout: `ciphertext || 16-byte-tag`. `DariPassphrase.Dispose()` zeroes key material via `CryptographicOperations.ZeroMemory`.

---

## Dari.App — GUI Application

Avalonia 11 desktop application targeting Windows, macOS, and Linux from a single codebase.
Phases A–D are complete (✅). Phases E–G are planned.

### Completed Phases

| Phase | Description |
|---|---|
| A | Project setup — skeleton, Fluent theme, drag-and-drop, icon |
| B | Archive browser — open `.dar`, DataGrid, search, flat + tree view, sort |
| C | Extraction — selected entries and full archive, progress dialog, conflict/checksum handling |
| D | Archive creation — three-step wizard (source → options → destination), progress, gitignore preview |
| i18n | Resource-based UI strings; English + Russian; `LocalizationManager`; Settings dialog with language+theme |

### Planned Phases

| Phase | Description |
|---|---|
| E | Archive appending via `ArchiveAppender` |
| F | In-pane file preview (text, images, hex dump) |
| G | Platform integration — file associations, native menus, code signing |

### Project Structure

```
Dari.App/
├── Assets/
│   ├── Locales/              # Avalonia ResourceDictionary per language
│   │   ├── en.axaml          # English strings
│   │   └── ru.axaml          # Russian strings
│   └── dari.ico / .png / .svg
├── Helpers/
│   └── DisplayFormatter.cs  # Human-readable size/ratio formatting
├── Models/
│   ├── AppConfig.cs          # Persisted settings (Language, Theme)
│   └── ArchiveEntryViewModel.cs
├── Services/
│   ├── IConfigService.cs / ConfigService.cs    # JSON config (~/.config/dari/config.json)
│   ├── IDialogService.cs / DialogService.cs    # File pickers, modal dialogs
│   ├── NullDialogService.cs                    # No-op implementation for tests
│   ├── ILocalizationManager.cs                 # LanguageItem, ThemeItem records + interface
│   └── LocalizationManager.cs                  # Singleton; swaps MergedDictionaries at runtime
├── ViewModels/
│   ├── MainWindowViewModel.cs
│   ├── ArchiveBrowserViewModel.cs   # Browse, search, sort, flat/tree view, FlatPaths toggle
│   ├── CreateArchiveViewModel.cs    # Three-step wizard
│   ├── ExtractViewModel.cs          # Extraction progress + flat-path logic
│   ├── PasswordPromptViewModel.cs
│   ├── SettingsViewModel.cs         # Language + Theme selection
│   ├── TreeNodeViewModel.cs         # DirectoryNodeViewModel (with IsSelected propagation)
│   └── ...
└── Views/
    ├── MainWindow.axaml / .cs
    ├── ArchiveBrowserView.axaml     # DataGrid + TreeView with directory checkboxes
    ├── CreateArchiveView.axaml
    ├── ExtractView.axaml
    ├── SettingsView.axaml           # Language + Theme dropdowns
    └── ...
```

### App Coding Conventions
- **MVVM via CommunityToolkit.Mvvm** — `[ObservableProperty]`, `[RelayCommand]`; no business logic in code-behind.
- **Compiled bindings** by default (`AvaloniaUseCompiledBindingsByDefault=true`); records used instead of tuples for `ItemTemplate` binding targets (e.g. `LanguageItem`, `ThemeItem`).
- **`DynamicResource`** for all user-visible strings; locale files swapped at runtime via `LocalizationManager`.
- **`ConfigureAwait(true)`** in UI-layer async methods (commands, dialogs); `ConfigureAwait(false)` in library code.
- **`CancellationTokenSource.Cancel()` before `Dispose()`** — always cancel before disposing a CTS to ensure running tasks wind down cleanly on window close.
- Config stored at `~/.config/dari/config.json` (Linux/macOS) or `%LOCALAPPDATA%\dari\config.json` (Windows); written on first run with detected system locale.

### Key App Dependencies
| Package | Use |
|---|---|
| `Avalonia` 11.x | UI framework |
| `Avalonia.Themes.Fluent` | Fluent design theme (light + dark) |
| `Avalonia.Controls.DataGrid` | Archive entry grid |
| `Avalonia.Desktop` | Native file dialogs |
| `CommunityToolkit.Mvvm` 8.x | MVVM source generators |
| `Dari.Archiver` | Project reference |
