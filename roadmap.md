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

### Phase E — Archive appending ✅

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

**Goal:** Add a resizable right-side preview pane to `ArchiveBrowserView` that renders text, syntax-highlighted code, images for selected archive entries — without full extraction — plus an "Extract & Open" action.

**Overall approach:** extend `ArchiveReader` with a capped read helper; build a `PreviewViewModel` state machine with async debounce via `CancellationTokenSource` + `Task.Delay`; add AvaloniaEdit + TextMate for syntax highlighting; wire DataGrid/TreeView selection to a new `SelectedEntry` property; split `ArchiveBrowserView` into a two-column `Grid` with a `GridSplitter`.

**Key components:**

- `PreviewViewModel` — reads the raw block via `ArchiveReader.OpenRawBlockAsync`, decodes on the fly; capped at 1 MB
- Preview types:
  - **Text** — UTF-8 / Latin-1 / Windows-1251 for text; display in plain `TextBlock`
  - **Code** - UTF-8 / Latin-1 / Windows-1251. Should have syntax highlight (support different filetypes of XML, like csproj, slnx, etc...)
  - **Markdown** — render `.md` files as markdown
  - **Images** — `Bitmap` via Avalonia (`png`, `jpg`, `bmp`, `gif`, `webp`)
  - **Other** — message about file is binary
- Preview pane to the right of the entry list; updates on selection change (250 ms debounce)
- "Extract & Open" button — extracts to a temp folder and opens with the system default app
- To detect file type - reimplement `classify_bytes` from /mnt/dev/dari/src/tui/preview.rs

Work on this phase step-by-step. After each step you should stop and ask for confirmation before moving on to the next one. This phase is more complex than the previous ones, so it should be broken down into smaller steps.

After each step there should be a working implementation of a feature, testable by user in the interface of the app, even if it's not fully complete yet. For example, after step 1, the preview pane should be able to show raw bytes of the selected entry, even if it doesn't have syntax highlighting or image rendering yet.

### Implementation Plan

#### Step 1 — Library extension + pane skeleton (selection → Loading / Binary placeholder)

Every subsequent step depends on this wiring. After this step, clicking any entry shows "Loading…" then "Binary file (N bytes)" (or an encrypted/error label) in the right pane.

**Files to create / modify:**

- **`Dari.Archiver/Archiving/ArchiveReader.cs`** — add `public async ValueTask<ReadOnlyMemory<byte>> ReadDecompressedPreviewAsync(IndexEntry entry, int maxBytes = 1 << 20, CancellationToken ct = default)`:
  - Reads the raw compressed block from the stream.
  - If the entry is encrypted: throws `InvalidOperationException("Entry is encrypted")` (caller maps to `Encrypted` state).
  - If `Compression == None`: returns `rawBlock[..Math.Min(rawBlock.Length, maxBytes)]`.
  - Otherwise: fully decompresses, then slices to `maxBytes`.

- **`Dari.App/ViewModels/PreviewViewModel.cs`** *(create)*:
  - `enum PreviewState { Empty, Loading, Text, Code, Image, Markdown, Binary, Error, Encrypted }`
  - Holds a reference to `ArchiveReader` (passed in from `ArchiveBrowserViewModel`).
  - `[ObservableProperty] PreviewState State`, `[ObservableProperty] string StatusMessage`.
  - `public void LoadAsync(ArchiveEntryViewModel? entry)` — CTS debounce pattern:
    ```
    _loadCts?.Cancel(); _loadCts?.Dispose(); _loadCts = new CancellationTokenSource();
    await Task.Delay(250, ct);   // debounce
    State = Loading;
    await LoadContentAsync(entry, ct);
    ```
  - `LoadContentAsync` (Step 1 body): calls `ReadDecompressedPreviewAsync`, sets `State = Binary`, `StatusMessage = $"Binary file ({bytes.Length} B)"`. Maps `InvalidOperationException` → `Encrypted` state, any other exception → `Error` state.
  - `Dispose()`: `_loadCts?.Cancel(); _loadCts?.Dispose()`.

- **`Dari.App/ViewModels/ArchiveBrowserViewModel.cs`**:
  - Add `public PreviewViewModel Preview { get; }` — constructed in ctor with current `ArchiveReader`.
  - Add `[ObservableProperty] private ArchiveEntryViewModel? _selectedEntry`.
  - `partial void OnSelectedEntryChanged(ArchiveEntryViewModel? value) => Preview.LoadAsync(value)`.
  - Dispose `Preview` inside existing `Dispose()` / `DisposeAsync()`.

- **`Dari.App/Views/PreviewView.axaml`** + **`PreviewView.axaml.cs`** *(create)*:
  - Root: `x:DataType="vm:PreviewViewModel"`.
  - State panels (visibility-switched via `IsVisible`):
    - **Empty** — centered dim label (`{DynamicResource Preview.Empty}`).
    - **Loading** — spinner / `ActivityIndicator` + loading label.
    - **Binary / Error / Encrypted** — centered `TextBlock Text="{Binding StatusMessage}"`.
  - `StatusMessage` label pinned to the bottom of the pane (visible in all non-empty states).

- **`Dari.App/Views/ArchiveBrowserView.axaml`** — restructure the entry-list row into a two-column `Grid`:
  ```xml
  <Grid ColumnDefinitions="*,4,300">
      <!-- existing DataGrid / TreeView Panel in Column 0 -->
      <GridSplitter Grid.Column="1" ResizeDirection="Columns" />
      <views:PreviewView Grid.Column="2" DataContext="{Binding Preview}" />
  </Grid>
  ```

- **`Dari.App/Views/ArchiveBrowserView.axaml.cs`** — wire selection in code-behind:
  ```csharp
  FlatGrid.SelectionChanged += (_, _) => SyncEntry(FlatGrid.SelectedItem as ArchiveEntryViewModel);
  TreeViewControl.SelectionChanged += (_, _) => SyncEntry((TreeViewControl.SelectedItem as FileNodeViewModel)?.Entry);
  void SyncEntry(ArchiveEntryViewModel? e) { if (DataContext is ArchiveBrowserViewModel vm) vm.SelectedEntry = e; }
  ```

- **`en.axaml`** / **`ru.axaml`** — add keys: `Preview.Empty`, `Preview.Loading`, `Preview.Binary`, `Preview.Encrypted`, `Preview.Error`.

---

#### Step 2 — Configurable preview size cap

After this step, the maximum number of bytes loaded for preview is stored in `AppConfig` and exposed in the Settings dialog. Default is **10 MB**. All subsequent steps read the cap from `PreviewViewModel._maxPreviewMegaBytes` rather than any hardcoded constant.

**Files to create / modify:**

- **`Dari.App/Models/AppConfig.cs`** — add:
  ```csharp
  public int PreviewMaxMegaBytes { get; set; } = 10;
  ```

- **`Dari.App/ViewModels/SettingsViewModel.cs`** — add:
  - `[ObservableProperty] private int _previewMaxMb` — in MB for the UI spinner.
  - Load in ctor: `_previewMaxMb = Math.Clamp(_configService.Config.PreviewMaxMegaBytes`.
  - In `SaveSettings` (or `ApplySettings`): `_configService.Config.PreviewMaxMegaBytes; await _configService.SaveAsync()`.

- **`Dari.App/Views/SettingsView.axaml`** — add a row in the settings grid:
  ```xml
  <TextBlock Text="{DynamicResource Settings.PreviewMaxBytes}" />
  <NumericUpDown Value="{Binding PreviewMaxMb}"
                 Minimum="1" Maximum="512"
                 FormatString="0 MB" />
  ```

- **`Dari.App/ViewModels/PreviewViewModel.cs`** — add `private readonly int _maxPreviewMegaBytes` constructor parameter. Pass it through to `ReadDecompressedPreviewAsync(entry, _maxPreviewBytes, ct)` and to all `ContentClassifier` calls (Steps 3–7).

- **`Dari.App/ViewModels/ArchiveBrowserViewModel.cs`** — pass `_configService.Config.PreviewMaxBytes` when constructing `PreviewViewModel`. If the user changes the setting and saves, recreate `PreviewViewModel` (or expose `MaxPreviewBytes` as a settable property on it).

- **`en.axaml`** / **`ru.axaml`** — add: `Settings.PreviewMaxBytes` ("Preview size limit"), `Settings.PreviewMaxBytesMb` ("{0} MB").

---

#### Step 3 — `ContentClassifier` + plain-text rendering

After this step, text/source files that don't yet have syntax highlighting show decoded content in a monospace `TextBlock` inside a `ScrollViewer`.

**`classify_bytes` — exact port of the Rust implementation** (`src/tui/preview.rs`, lines 182–234):

The algorithm operates on a preview slice capped at the configurable `PreviewMaxBytes` limit (default 10 MB, set in Settings — see Step 2). The classification order and thresholds are:

1. **Null byte** — if any `0x00` appears in the preview slice → **Binary**.
2. **Control-byte ratio** — count bytes where `b < 0x09 || (b > 0x0D && b < 0x20)` (i.e. `0x01–0x08` and `0x0E–0x1F`; TAB/LF/VT/FF/CR are allowed). If `ctrl × 10 > previewLength` (> 10 %) → **Binary**.
3. **Strict UTF-8** — `Encoding.UTF8.GetString` with `DecoderFallback.ExceptionFallback`; if it succeeds → **Text (UTF-8)** (further routed to `Code`/`Markdown`/`Text` by extension).
4. **Windows-1251 fallback** — `Encoding.GetEncoding(1251, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback).GetString(...)`; if it succeeds → **Text (Windows-1251)**.
5. Otherwise → **Binary**.

> **Note:** The Rust `classify_bytes` does **not** detect images — images fall through as Binary (null bytes). Image detection in the app is handled **separately** in `PreviewViewModel` by file extension + Avalonia's `Bitmap` constructor (added in Step 4). `ContentClassifier` is a pure text/binary classifier and contains no image logic.

> **Empty bytes** — empty input has no null bytes, no control bytes, and is valid UTF-8 → classified as **Text (UTF-8)**. `PreviewState.Empty` is only used when *no entry is selected*, not for zero-byte files.

**Files to create / modify:**

- **`Dari.App/Helpers/ContentClassifier.cs`** *(create)*:
  - `enum ContentKind { Text, Binary }` (no image — detection is Avalonia's job via extension+`Bitmap`; empty bytes → Text)
  - `record struct ClassifyResult(ContentKind Kind, string Encoding, bool Truncated)`
  - `static ClassifyResult ClassifyBytes(ReadOnlySpan<byte> bytes, int maxBytes)` — exact port of Rust `classify_bytes`:
    1. Compute preview slice: `bytes.Length > maxBytes ? bytes[..maxBytes] : bytes`; set `truncated = bytes.Length > maxBytes`.
    2. Null-byte check on preview slice → `Binary`.
    3. Control-byte ratio on preview: count `b < 0x09 || (b > 0x0D && b < 0x20)`; if `ctrl * 10 > preview.Length` → `Binary`.
    4. Try strict UTF-8 (`DecoderFallback.ExceptionFallback`): success → `Text`, encoding `"UTF-8"`.
    5. Try Windows-1251 with exception fallback: success → `Text`, encoding `"Windows-1251"`.
    6. → `Binary`.
  - `static PreviewState ClassifyForPreview(ReadOnlySpan<byte> bytes, string extension, int maxBytes)`:
    - `Binary` → `PreviewState.Binary`.
    - `Text` with `.md` extension → `PreviewState.Markdown`.
    - `Text` with known code extensions → `PreviewState.Code`.
    - `Text` (all others, including empty bytes) → `PreviewState.Text`.
    - Known code extensions: `.cs .fs .vb .xml .axaml .csproj .slnx .props .targets .json .toml .yaml .yml .py .rs .go .js .mjs .ts .sh .bash .fish .c .cpp .h .java .kt .rb .php .html .css .sql .dart .swift .zig`
  - `static string DecodeText(ReadOnlySpan<byte> bytes, string encoding)` — uses `encoding == "Windows-1251"` → `Encoding.GetEncoding(1251).GetString(bytes)`; otherwise → `Encoding.UTF8.GetString(bytes)`. Called only after `ClassifyBytes` confirms the encoding.

- **`Dari.App/ViewModels/PreviewViewModel.cs`**:
  - Add `[ObservableProperty] string PreviewText`.
  - In `LoadContentAsync`: call `ContentClassifier.ClassifyForPreview(bytes.Span, entry.Extension, _maxPreviewBytes)`.
  - For `Text` state: call `ContentClassifier.DecodeText(bytes.Span, result.Encoding)`, store in `PreviewText`, set `State = Text`. Set `StatusMessage = Loc("Preview.Truncated")` if `result.Truncated`.
  - `Code` and `Markdown` states: also decode and store in `PreviewText` (highlighting/rendering added in later steps); temporarily set `State = Text` so something readable appears — corrected in Steps 5 and 6.

- **`Dari.App/Views/PreviewView.axaml`** — add text panel:
  ```xml
  <ScrollViewer IsVisible="{Binding State, Converter={x:Static ObjectConverters.Equal}, ConverterParameter={x:Static vm:PreviewState.Text}}"
                HorizontalScrollBarVisibility="Auto">
      <TextBlock FontFamily="Courier New, Cascadia Code, monospace"
                 FontSize="12"
                 Text="{Binding PreviewText}"
                 TextWrapping="NoWrap" />
  </ScrollViewer>
  ```
  - Use the same `IsVisible` pattern for all state panels throughout the phase.

- **`en.axaml`** / **`ru.axaml`** — add: `Preview.Truncated` ("Showing first {0} MB"), `Preview.Text`. (The `{0}` placeholder is filled with `PreviewMaxBytes / 1_048_576` at runtime.)

---

#### Step 4 — Image preview

After this step, `.png`, `.jpg`/`.jpeg`, `.bmp`, `.gif`, and `.webp` files render as images in the pane. Detection is **extension-based** — Avalonia's `Bitmap` constructor handles format validation internally; no magic-byte sniffing is needed.

**Files to create / modify:**

- **`Dari.App/ViewModels/PreviewViewModel.cs`**:
  - Add `[ObservableProperty] Bitmap? PreviewBitmap`.
  - Add a private static set: `private static readonly FrozenSet<string> ImageExtensions = FrozenSet.ToFrozenSet([".png", ".jpg", ".jpeg", ".bmp", ".gif", ".webp"])`.
  - In `LoadContentAsync`, **before** calling `ContentClassifier.ClassifyForPreview`:
    - If `ImageExtensions.Contains(entry.Extension.ToLowerInvariant())`:
      ```csharp
      try
      {
          var old = PreviewBitmap;
          using var ms = new MemoryStream(bytes.ToArray());
          PreviewBitmap = new Bitmap(ms);
          old?.Dispose();
          State = PreviewState.Image;
          return;  // skip text classification
      }
      catch (Exception ex)
      {
          State = PreviewState.Error;
          StatusMessage = $"Failed to decode image: {ex.Message}";
          return;
      }
      ```
    - Otherwise → fall through to `ContentClassifier.ClassifyForPreview`.
  - Dispose `PreviewBitmap` in `Dispose()`.

- **`Dari.App/Views/PreviewView.axaml`** — add image panel:
  ```xml
  <ScrollViewer IsVisible="{Binding State, ..., ConverterParameter={x:Static vm:PreviewState.Image}}"
                HorizontalScrollBarVisibility="Auto"
                VerticalScrollBarVisibility="Auto">
      <Image Source="{Binding PreviewBitmap}" Stretch="None" />
  </ScrollViewer>
  ```

- **`en.axaml`** / **`ru.axaml`** — add: `Preview.Image`.

---

#### Step 5 — Syntax-highlighted code preview (AvaloniaEdit + TextMate)

After this step, source code files display with proper syntax highlighting (colours, keywords, strings, comments).

**New NuGet packages:**

| Package | Version | Purpose |
|---------|---------|---------|
| `AvaloniaEdit` | ≥ 11.1.0 | Code editor control + `AvaloniaEdit.TextMate` integration |
| `TextMateSharp.Grammars` | ≥ 3.0.x | Bundled TextMate grammar definitions |

**Files to create / modify:**

- **`Dari.App/Dari.App.csproj`** — add the two packages above.

- **`Dari.App/Helpers/ExtensionLanguageMap.cs`** *(create)*:
  - `static string? GetScopeByExtension(string ext)` — maps file extension to TextMate scope name:
    - `.cs` → `"source.cs"`, `.fs` → `"source.fsharp"`, `.vb` → `"source.asp.vb.net"`
    - `.xml` / `.axaml` / `.csproj` / `.slnx` / `.props` / `.targets` / `.xaml` → `"text.xml"`
    - `.json` → `"source.json"`, `.toml` → `"source.toml"`, `.yaml` / `.yml` → `"source.yaml"`
    - `.py` → `"source.python"`, `.rs` → `"source.rust"`, `.go` → `"source.go"`
    - `.js` / `.mjs` → `"source.js"`, `.ts` → `"source.ts"`
    - `.sh` / `.bash` → `"source.shell"`, `.fish` → `"source.shell"`
    - `.c` / `.h` → `"source.c"`, `.cpp` → `"source.cpp"`, `.java` → `"source.java"`, `.kt` → `"source.kotlin"`
    - `.rb` → `"source.ruby"`, `.php` → `"source.php"`, `.html` → `"text.html.basic"`, `.css` → `"source.css"`
    - `.sql` → `"source.sql"`, `.dart` → `"source.dart"`, `.swift` → `"source.swift"`, `.zig` → `"source.zig"`
    - Returns `null` for unknown extensions (pane falls back to plain-text display).

- **`Dari.App/ViewModels/PreviewViewModel.cs`**:
  - Add `[ObservableProperty] string? TextMateScope`.
  - In `LoadContentAsync` for `Code`: decode text, store in `PreviewText`, set `TextMateScope = ExtensionLanguageMap.GetScopeByExtension(ext)`, then `State = Code`.

- **`Dari.App/Views/PreviewView.axaml`** — add Code panel:
  ```xml
  <!-- xmlns:aedit="using:AvaloniaEdit" -->
  <aedit:TextEditor x:Name="CodeEditor"
                    IsVisible="{Binding State, ..., ConverterParameter={x:Static vm:PreviewState.Code}}"
                    IsReadOnly="True"
                    ShowLineNumbers="True"
                    FontFamily="Courier New, Cascadia Code, monospace"
                    FontSize="12"
                    WordWrap="False" />
  ```

- **`Dari.App/Views/PreviewView.axaml.cs`** — TextMate wiring in code-behind (cannot be done purely via compiled bindings):
  ```csharp
  private IRegistryOptions? _registryOptions;
  private TextMateInstallation? _textMate;

  protected override void OnDataContextChanged(EventArgs e)
  {
      base.OnDataContextChanged(e);
      if (DataContext is not PreviewViewModel vm) return;
      var theme = /* read from IConfigService */ isDark ? ThemeName.DarkPlus : ThemeName.LightPlus;
      _registryOptions ??= new RegistryOptions(theme);
      _textMate ??= CodeEditor.InstallTextMate(_registryOptions);
      vm.PropertyChanged += OnVmPropertyChanged;
  }

  private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
  {
      if (DataContext is not PreviewViewModel vm) return;
      if (e.PropertyName is nameof(PreviewViewModel.PreviewText) or nameof(PreviewViewModel.TextMateScope)
          && vm.State == PreviewState.Code)
      {
          CodeEditor.Document = new TextDocument(vm.PreviewText ?? "");
          if (vm.TextMateScope is { } scope)
              _textMate?.SetGrammar(scope);
      }
  }
  ```
  - Subscribe/unsubscribe `PropertyChanged` cleanly on `DataContext` change to avoid leaks.

- **`en.axaml`** / **`ru.axaml`** — add: `Preview.Code`.

---

#### Step 6 — Markdown preview

After this step, `.md` files render as formatted Markdown (headers, lists, inline code, bold/italic).

**New NuGet package:**

| Package | Version | Purpose |
|---------|---------|---------|
| `Markdown.Avalonia` | ≥ 11.x | `MarkdownScrollViewer` Avalonia control |

**Files to create / modify:**

- **`Dari.App/Dari.App.csproj`** — add `Markdown.Avalonia`.

- **`Dari.App/Views/PreviewView.axaml`** — add Markdown panel:
  ```xml
  <!-- xmlns:mda="using:Markdown.Avalonia" -->
  <mda:MarkdownScrollViewer
      IsVisible="{Binding State, ..., ConverterParameter={x:Static vm:PreviewState.Markdown}}"
      Markdown="{Binding PreviewText}" />
  ```
  - No `PreviewViewModel` changes needed — `PreviewText` is already populated by Step 3 for Markdown state.

- **`en.axaml`** / **`ru.axaml`** — add: `Preview.Markdown`.

---

#### Step 7 — "Extract & Open" action

After this step, any selected entry can be opened in the system default application directly from the preview pane.

**Files to create / modify:**

- **`Dari.App/ViewModels/PreviewViewModel.cs`**:
  - Add `[ObservableProperty][NotifyCanExecuteChangedFor(nameof(ExtractAndOpenCommand))] ArchiveEntryViewModel? CurrentEntry`.
  - Assign `CurrentEntry = entry` at the start of `LoadContentAsync`; null it on cancel / empty.
  - Add command:
    ```csharp
    [RelayCommand(CanExecute = nameof(CanExtractAndOpen))]
    private async Task ExtractAndOpenAsync(CancellationToken ct)
    {
        var entry = CurrentEntry!;
        var tempDir = Path.Combine(Path.GetTempPath(), "dari-preview");
        Directory.CreateDirectory(tempDir);
        var dest = Path.Combine(tempDir, Path.GetFileName(entry.Entry.Path));
        await _reader.ExtractToFileAsync(entry.Entry, dest, ct: ct).ConfigureAwait(true);
        Process.Start(new ProcessStartInfo { FileName = dest, UseShellExecute = true });
    }
    private bool CanExtractAndOpen() =>
        CurrentEntry is not null && State is not PreviewState.Empty and not PreviewState.Loading;
    ```
  - Catch all exceptions; surface as `StatusMessage`.

- **`Dari.App/Views/PreviewView.axaml`** — add toolbar row at the top of the pane:
  ```xml
  <Grid RowDefinitions="Auto,*,Auto">
      <Button Grid.Row="0" HorizontalAlignment="Right" Margin="4"
              Content="{DynamicResource Button.ExtractAndOpen}"
              Command="{Binding ExtractAndOpenCommand}"
              ToolTip.Tip="{DynamicResource Tooltip.ExtractAndOpen}" />
      <!-- state panels in Grid.Row="1" -->
      <TextBlock Grid.Row="2" Text="{Binding StatusMessage}" Margin="4,2"
                 Foreground="{DynamicResource SystemControlForegroundBaseMediumBrush}" />
  </Grid>
  ```

- **`en.axaml`** / **`ru.axaml`** — add: `Button.ExtractAndOpen` ("Extract & Open"), `Tooltip.ExtractAndOpen` ("Extract to a temporary folder and open with the default application").

---

#### NuGet packages added in this phase

| Step | Package | Purpose |
|------|---------|---------|
| 5 | `AvaloniaEdit` ≥ 11.1.0 | Syntax-highlighting code editor + TextMate integration |
| 5 | `TextMateSharp.Grammars` ≥ 3.0.x | Bundled TextMate grammar definitions |
| 6 | `Markdown.Avalonia` ≥ 11.x | `MarkdownScrollViewer` control |

#### Further considerations

- **Large-file decompression**: `ReadDecompressedPreviewAsync` fully decompresses before slicing to the configurable cap (default 10 MB). For very large compressed entries this wastes CPU. A follow-up optimisation can use a `CappedBufferWriter<byte>` implementing `IBufferWriter<byte>` that aborts after `maxBytes` written.
- **AvaloniaEdit theme sync**: `RegistryOptions` takes `ThemeName` at construction. Read `AppConfig.Theme` from `IConfigService` to pick `ThemeName.LightPlus` or `ThemeName.DarkPlus`; recreate `_textMate` if the user switches theme at runtime.
- **DataGrid multi-select coexistence**: The DataGrid keeps `SelectionMode="Extended"` for checkbox-based multi-row extraction. `SelectedItem` (focused-row) is synced to `SelectedEntry` only via the code-behind `SyncEntry` helper — `SelectedEntry` must **not** mutate `ArchiveEntryViewModel.IsSelected` to avoid breaking checkbox state.
- **Windows-1251 on .NET**: `Encoding.GetEncoding(1251)` requires `System.Text.Encoding.CodePages` to be registered (`Encoding.RegisterProvider(CodePagesEncodingProvider.Instance)`) on non-Windows platforms. Call this once in `App.axaml.cs` on startup.
- **Image extension vs. content**: Avalonia's `Bitmap` constructor will throw for a corrupt or misnamed file — the `Error` state is shown in that case. There is intentionally no fallback to text classification for misnamed image extensions.

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
