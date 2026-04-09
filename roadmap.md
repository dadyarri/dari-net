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

**Goal:** Add a resizable right-side preview pane to `ArchiveBrowserView` that renders text, syntax-highlighted code, images, and hex dumps for selected archive entries — without full extraction — plus an "Extract & Open" action.

**Overall approach:** extend `ArchiveReader` with a capped read helper; build a `PreviewViewModel` state machine with async debounce via `CancellationTokenSource` + `Task.Delay`; add AvaloniaEdit + TextMate for syntax highlighting; wire DataGrid/TreeView selection to a new `SelectedEntry` property; split `ArchiveBrowserView` into a two-column `Grid` with a `GridSplitter`.

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

#### Step 1 — Add NuGet packages to `Dari.App.csproj`

Add `AvaloniaEdit` (11.x, the Avalonia port of AvalonEdit), `AvaloniaEdit.TextMate`, and `TextMateSharp.Grammars`.

These three together provide VS Code-quality syntax highlighting for 200+ languages. Note: `TextMateSharp.Grammars` bundles all VS Code grammars (~15 MB). If binary size is a concern, AvaloniaEdit's built-in XSHD highlighting engine (much lighter, ~30 common languages) is the alternative — decide before implementation (see **Further Considerations**).

---

#### Step 2 — Add `ReadPreviewAsync` to `Dari.Archiver/Archiving/ArchiveReader.cs`

New public method:
```csharp
ValueTask<(ReadOnlyMemory<byte> Data, bool IsTruncated)> ReadPreviewAsync(
    IndexEntry entry, int maxBytes = 1_048_576, CancellationToken ct = default)
```

- Check `entry.OriginalSize > (ulong)maxBytes` first — if so, return `(default, true)` without reading anything (avoids decompressing huge files).
- Otherwise call the existing `ExtractAsync(entry, MemoryStream, verifyChecksum: true, ct)` and return the resulting bytes.

This is cleaner than using `OpenRawBlockAsync` (which returns still-compressed bytes) and requires no breaking changes to the existing API.

---

#### Step 3 — Create `Dari.App/Helpers/EncodingDetector.cs`

Static class `EncodingDetector` with `Detect(ReadOnlySpan<byte> data) : Encoding`. Detection order:

1. Check for UTF-8 BOM (`EF BB BF`) → return `Encoding.UTF8`.
2. Try `Encoding.UTF8.GetCharCount(data)` (no exception) → valid UTF-8, return `Encoding.UTF8`.
3. Windows-1251 heuristic: count bytes in `0xC0–0xFF` (Windows-1251 Cyrillic range) — if > 2% of data, return `Encoding.GetEncoding(1251)`.
4. Fallback to `Encoding.Latin1`.

> **Note:** `Encoding.GetEncoding(1251)` requires `System.Text.Encoding.CodePages` and a call to `Encoding.RegisterProvider(CodePagesEncodingProvider.Instance)` in `App.axaml.cs` at startup (needed on non-Windows).

---

#### Step 4 — Create `Dari.App/Helpers/HexDumpFormatter.cs`

Static class `HexDumpFormatter` with `Format(ReadOnlySpan<byte> data, int maxBytes = 512) : string`.

- Emits the classic `OFFSET | HH HH HH ... (16 bytes) | ASCII` format using a `StringBuilder`.
- Use `stackalloc char[16]` for the ASCII column; replace non-printable bytes with `.`.
- Limit output to `maxBytes` before formatting.
- Returns `string.Empty` for zero-length input.

---

#### Step 5 — Create `Dari.App/ViewModels/PreviewViewModel.cs`

##### Enums (declared in the same file)
```csharp
public enum PreviewState { Idle, Loading, Ready, Error, TooLarge }
public enum PreviewKind  { None, Text, Code, Image, Hex }
```

##### Class: `sealed partial class PreviewViewModel : ObservableObject, IDisposable`
Constructor takes `ArchiveReader reader`.

**Observable properties:**
| Property | Type | Purpose |
|---|---|---|
| `State` | `PreviewState` | Current display state |
| `Kind` | `PreviewKind` | Which content panel to show |
| `TextContent` | `string?` | Decoded text (Text + Code paths) |
| `ImageSource` | `Bitmap?` | Decoded bitmap |
| `HexContent` | `string?` | Formatted hex dump |
| `ErrorMessage` | `string?` | Exception message when `State == Error` |
| `IsTruncated` | `bool` | True when file exceeds 1 MB |
| `CurrentEntry` | `ArchiveEntryViewModel?` | Currently previewed entry |
| `SyntaxLanguage` | `string?` | TextMate language ID (e.g. `"csharp"`) |

**Debounce — `LoadAsync(ArchiveEntryViewModel? entry)`:**
1. Cancel + dispose the previous `_loadCts`.
2. Create a new `CancellationTokenSource` stored in `_loadCts`.
3. `await Task.Delay(250, ct).ConfigureAwait(true)`.
4. Set `State = Loading`.
5. Proceed to routing logic below.

**Routing logic (after the 250 ms delay):**

| Condition | Action |
|---|---|
| `entry` is `null` | `Kind = None, State = Idle` |
| `entry.OriginalSize == 0` | `Kind = None, State = Idle` |
| Extension in image set (`.png .jpg .jpeg .bmp .gif .webp`) | `ReadPreviewAsync` → `new Bitmap(MemoryStream)` → `Kind = Image` |
| Extension in plain-text set (`.txt .md .log .csv .ini .toml .yaml .yml .json .xml .html .css`) | `ReadPreviewAsync` → `EncodingDetector.Detect` → decode → `Kind = Text` |
| Extension in code set (`.cs .fs .py .js .ts .rs .go .cpp .c .h .java .sh .ps1 …`) | Same as text but also set `SyntaxLanguage` → `Kind = Code` |
| Anything else | `ReadPreviewAsync(maxBytes: 512)` → `HexDumpFormatter.Format` → `Kind = Hex` |
| `IsTruncated == true` | `State = TooLarge` (no content populated) |

All paths wrap in `try/catch`, setting `State = Error` + `ErrorMessage` on failure.

**`[RelayCommand] ExtractAndOpenAsync()`:**
1. Extract `CurrentEntry` to `Path.Combine(Path.GetTempPath(), "dari-preview", Guid.NewGuid().ToString("N"), fileName)` via `_reader.ExtractToFileAsync(...)`.
2. `Process.Start(new ProcessStartInfo { FileName = tempPath, UseShellExecute = true })`.
3. Track temp paths in a `List<string>`; attempt to delete them in `Dispose()`, swallowing `IOException` silently (external app may still have the file open on Windows).

> Note: "Extract & Open" opens a file in an external app that may lock it; deleting in `Dispose()` may fail on Windows. Solution: swallow `IOException` silently and leave cleanup to the OS temp folder purge.

**`Dispose()`:**
```csharp
_loadCts?.Cancel();
_loadCts?.Dispose();
ImageSource?.Dispose();
// delete temp files (swallow IOException)
```

---

#### Step 6 — Extend `Dari.App/ViewModels/ArchiveBrowserViewModel.cs`

- Add `[ObservableProperty] private ArchiveEntryViewModel? _selectedEntry`.
- Add `public PreviewViewModel Preview { get; }` initialized in the constructor (with `_reader`).
- In `partial void OnSelectedEntryChanged(ArchiveEntryViewModel? value)` → call `_ = Preview.LoadAsync(value)` (fire-and-forget; `LoadAsync` manages its own cancellation).
- In `Dispose()` / `DisposeAsync()` → add `Preview.Dispose()`.

---

#### Step 7 — Create `Dari.App/Views/PreviewView.axaml` + `PreviewView.axaml.cs`

##### AXAML layout

Root: `UserControl` with `x:DataType="vm:PreviewViewModel"`.

```
Grid RowDefinitions="Auto,*"
├── Row 0 — Header bar
│   ├── TextBlock — CurrentEntry.Name
│   ├── Border (badge) — "Truncated" (IsVisible=IsTruncated)
│   └── Button — "Extract & Open" (Command=ExtractAndOpenCommand, IsEnabled=CurrentEntry!=null)
└── Row 1 — Content panel (Panel with overlapping children, each IsVisible-bound)
    ├── ProgressBar IsIndeterminate — State == Loading
    ├── TextBlock (centered, muted) — State == Idle  →  "Select a file to preview"
    ├── TextBlock (error) — State == Error  →  ErrorMessage
    ├── TextBlock (too large) — State == TooLarge
    ├── ScrollViewer + TextBlock (monospace, NoWrap) — Kind == Text  →  TextContent
    ├── aedit:TextEditor x:Name="CodeEditor"  — Kind == Code  (IsReadOnly, ShowLineNumbers)
    ├── ScrollViewer + Image — Kind == Image  →  Source=ImageSource
    └── ScrollViewer + TextBlock (monospace) — Kind == Hex  →  HexContent
```

> Use `x:CompileBindings="False"` on `TextEditor` because `TextEditor.Text` is a plain CLR property, not an Avalonia styled property.

##### Code-behind (`PreviewView.axaml.cs`)

- On `OnDataContextChanged`: subscribe to `PreviewViewModel.PropertyChanged`.
- In the `PropertyChanged` handler: when `Kind == Code` and `SyntaxLanguage` changes, call `_textMateInstallation.SetGrammar(registryOptions.GetScopeByLanguageId(SyntaxLanguage))` and set `CodeEditor.Text = TextContent ?? ""`. When `Kind == Text`, only update `TextContent` binding (handled by AXAML).
- Initialize TextMate once (lazily on first `Kind == Code`):
  ```csharp
  var registryOptions = new RegistryOptions(ThemeName.DarkPlus);
  _textMateInstallation = CodeEditor.InstallTextMate(registryOptions);
  ```
- On DataContext detach: `_textMateInstallation?.Dispose()`.
- **Theme synchronisation for AvaloniaEdit**: the TextMate theme (`DarkPlus`) should switch when the user toggles light/dark in Settings. Hook into `LocalizationManager.ThemeChanged` (or `ActualThemeVariant` on the root window) and call `_textMateInstallation.SetTheme(registryOptions.LoadTheme(newThemeName))`.

---

#### Step 8 — Modify `ArchiveBrowserView.axaml` + `ArchiveBrowserView.axaml.cs`

##### AXAML

Wrap the existing entry list in a new outer `Grid ColumnDefinitions="*,4,340"`:
- `Column 0` — existing entry list (DataGrid + TreeView).
- `Column 1` — `GridSplitter` (allows user to resize the pane).
- `Column 2` — `views:PreviewView DataContext="{Binding Preview}"`.

##### Code-behind

Hook selection events to update `vm.SelectedEntry`:

```csharp
FlatGrid.SelectionChanged += (_, e) => {
    if (e.AddedItems.Count > 0 && e.AddedItems[0] is ArchiveEntryViewModel entry)
        ((ArchiveBrowserViewModel)DataContext!).SelectedEntry = entry;
};
TreeViewControl.SelectionChanged += (_, e) => { /* similar, unwrap FileNodeViewModel */ };
```

---

#### Step 9 — Add i18n strings to locale files

Add to both `en.axaml` and `ru.axaml`:

| Key | English | Russian |
|---|---|---|
| `Preview.Loading` | Loading preview… | Загрузка… |
| `Preview.Empty` | Select a file to preview | Выберите файл для просмотра |
| `Preview.Error` | Failed to load preview | Не удалось загрузить предпросмотр |
| `Preview.TooLarge` | File exceeds 1 MB preview limit | Файл превышает лимит предпросмотра 1 МБ |
| `Preview.Truncated` | Preview capped at 1 MB | Предпросмотр ограничен 1 МБ |
| `Preview.Directory` | Directory — no preview | Каталог — предпросмотр недоступен |
| `Button.ExtractAndOpen` | Extract & Open | Извлечь и открыть |

---

#### Step 10 — Write unit tests

##### `Dari.App.Tests/EncodingDetectorTests.cs`
- UTF-8 BOM bytes → returns `Encoding.UTF8`.
- Pure ASCII bytes → returns `Encoding.UTF8`.
- Known Cyrillic UTF-8 bytes → returns `Encoding.UTF8`.
- Bytes valid in Windows-1251 but invalid in UTF-8, with > 2% Cyrillic bytes → returns `1251` (or `Latin1` per platform decision).
- Mixed-Latin bytes (no high bytes) → returns `Latin1`.

##### `Dari.App.Tests/HexDumpFormatterTests.cs`
- 16-byte input produces correct offset / hex / ASCII columns.
- Zero-length input produces `string.Empty`.
- 600-byte input truncates at 512 bytes (counts output lines × 16).

##### `Dari.App.Tests/PreviewViewModelTests.cs`
(Use a real `ArchiveReader` backed by a `MemoryStream` containing a programmatically-built `.dar` archive via `ArchiveWriter`.)
- Entry with `OriginalSize > 1 MB` → `State == TooLarge`.
- `.txt` entry → `State == Ready`, `Kind == Text`, `TextContent` is non-empty.
- `.png` entry → `Kind == Image`, `ImageSource` is non-null.
- Unknown extension entry → `Kind == Hex`, `HexContent` starts with `00000000`.
- Rapid two `LoadAsync` calls → only the second entry is reflected in the final state (debounce/cancellation).
- `ExtractAndOpenAsync` creates a temp file at the expected path.

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
