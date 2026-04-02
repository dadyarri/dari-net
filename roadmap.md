# Dari.Archiver — Implementation Roadmap

> **Target:** `Dari.Archiver` class library, .NET 10, C# 13  
> **Format:** Dari v5 (see `docs/src/archive_structure.md`)  
> **Goal:** A correct, high-performance, fully-managed reader/writer for `.dar` archives.

---

## 1. Goals

| Goal | Description |
|------|-------------|
| **Correctness** | Fully implement Dari v5 format: header, footer, index, data blocks, all bitflags |
| **Speed** | Zero-copy parsing, pipeline I/O, buffer pooling, parallel compression |
| **Modern APIs** | `System.IO.Pipelines`, `Span<T>`, `BinaryPrimitives`, `System.Buffers`, `ValueTask` |
| **Allocation efficiency** | `ArrayPool<T>`, stack-allocated structs, `MemoryMarshal`, avoid boxing |
| **Testability** | Pure, injectable interfaces; no static state |
| **Extensibility** | Pluggable compressor/decompressor registry, open `IExtraFieldEncoder` interface |

### Non-Goals (v1)

- CLI tooling — that lives in a separate project
- Image EXIF / audio tag extraction — extra-field *parsing* is supported, but writing these keys is left to callers
- Lepton JPEG recompression — no stable managed library exists; `LeptonJpeg` method is read-supported (pass-through) only
- Cross-archive deduplication

---

## 2. Architecture Overview

```
Dari.Archiver
├── Format/                    # Dari format constants, structs, enums
│   ├── DariConstants.cs        # Magic bytes, version, fixed sizes
│   ├── DariHeader.cs           # 13-byte header struct
│   ├── DariFooter.cs           # 15-byte footer struct
│   ├── IndexEntry.cs          # 85-byte fixed struct + variable fields
│   ├── CompressionMethod.cs   # enum: None=0, Brotli=1, Zstandard=2, Lzma=3, LeptonJpeg=4
│   └── IndexFlags.cs          # [Flags] enum: LinkedData=0x0001, EncryptedData=0x0002
│
├── IO/                        # Low-level binary I/O
│   ├── DariReader.cs           # Stream → parsed header/footer/index/data
│   ├── DariWriter.cs           # Writes header, data blocks, index, footer atomically
│   └── BinaryHelpers.cs       # Span-based LE read/write helpers (thin wrappers over BinaryPrimitives)
│
├── Compression/               # Compression pipeline
│   ├── ICompressor.cs         # interface: CompressAsync / DecompressAsync
│   ├── CompressorRegistry.cs  # Maps CompressionMethod enum → ICompressor; extension-to-method map
│   ├── NoneCompressor.cs      # Pass-through (copy)
│   ├── BrotliCompressor.cs    # System.IO.Compression.BrotliStream (quality=6, lgwin=22)
│   ├── ZstandardCompressor.cs # ZstdSharp (level=3)
│   └── LzmaCompressor.cs      # SharpCompress / SevenZipSharp (XZ container, preset=9)
│
├── Crypto/                    # Encryption/decryption
│   ├── DariEncryption.cs       # Key derivation, nonce derivation, encrypt/decrypt
│   └── DariPassphrase.cs       # Value object wrapping passphrase bytes; zeroes memory on Dispose
│
├── Deduplication/
│   └── DeduplicationTracker.cs  # checksum → data_offset map; detects linked entries
│
├── Extra/
│   ├── ExtraField.cs          # Parses/serialises semicolon-separated key=value pairs
│   └── WellKnownExtraKeys.cs  # Constants: "e", "en", "et", "imk", "imd", "idt", etc.
│
├── Archiving/                 # High-level API
│   ├── ArchiveReader.cs       # Open existing .dar; enumerate entries; extract files
│   ├── ArchiveWriter.cs       # Create new .dar with fluent builder
│   └── ArchiveAppender.cs     # Append new files to an existing .dar
│
└── Diagnostics/
    └── DariFormatException.cs  # Thrown on format violations (bad magic, bad version, etc.)
```

---

## 3. Dependencies

| Package | Version | Purpose |
|---------|---------|---------|
| `Blake3` (Blake3.NET) | latest | BLAKE3 checksums and KDF |
| `ZstdSharp.Port` | latest | Zstandard compression (pure managed, fast) |
| `SharpCompress` | latest | LZMA/XZ compression and decompression |
| `System.IO.Pipelines` | in-box (.NET 10) | High-throughput I/O pipeline |
| `System.IO.Compression` | in-box (.NET 10) | `BrotliStream`, `BrotliEncoder` / `BrotliDecoder` |
| `System.Security.Cryptography` | in-box (.NET 10) | `ChaCha20Poly1305` (added .NET 6) |
| `System.Buffers` | in-box (.NET 10) | `ArrayPool<T>`, `IBufferWriter<T>` |
| `System.Runtime.InteropServices` | in-box (.NET 10) | `MemoryMarshal` for zero-copy struct reads |
| `Microsoft.IO.RecyclableMemoryStream` | latest | Pooled `MemoryStream` for intermediate buffers |

---

## 4. Phase 1 — Format Primitives

**Goal:** Compile-time-correct, allocation-free representation of every Dari struct.

### 4.1 Constants

```csharp
// DariConstants.cs
internal static class DariConstants
{
    public static ReadOnlySpan<byte> HeaderMagic => "DARI"u8;
    public static ReadOnlySpan<byte> FooterMagic => "DARIEND"u8;
    public const byte FormatVersion = 5;
    public const int HeaderSize    = 13;
    public const int FooterSize    = 15;
    public const int IndexEntryFixedSize = 85;
    public const int MinArchiveSize = HeaderSize + FooterSize; // 28
}
```

### 4.2 Header struct

```csharp
// DariHeader.cs — 13 bytes, no padding
internal readonly struct DariHeader
{
    public readonly DateTimeOffset CreatedAt;   // parsed from u64 Unix epoch

    public static DariHeader ReadFrom(ReadOnlySpan<byte> span)   // span must be >= 13 bytes
    public void WriteTo(Span<byte> span)
}
```

- `signature[4]` — validated against `"DARI"u8`
- `version: u8` — must equal `5`; throw `DariFormatException` otherwise
- `timestamp: u64 LE`

### 4.3 Footer struct

```csharp
internal readonly struct DariFooter
{
    public readonly uint IndexOffset;
    public readonly uint FileCount;

    public static DariFooter ReadFrom(ReadOnlySpan<byte> span)
    public void WriteTo(Span<byte> span)
}
```

Validation rules (§5 of spec):
- File length ≥ 28
- `signature == "DARIEND"u8`
- `IndexOffset >= 13`
- `IndexOffset <= fileLength - 15`

### 4.4 Index entry

```csharp
internal readonly struct IndexEntryFixed   // exactly 85 bytes
{
    public readonly ulong  Offset;
    public readonly IndexFlags BitFlags;
    public readonly CompressionMethod Compression;
    public readonly ulong  ModificationTimestamp;
    public readonly uint   Uid;
    public readonly uint   Gid;
    public readonly ushort Perm;
    public readonly Blake3Hash Checksum;   // 32-byte value type
    public readonly ulong  OriginalSize;
    public readonly ulong  CompressedSize;
    public readonly uint   PathLength;
    public readonly uint   ExtraLength;
}
```

Use `MemoryMarshal.Read<IndexEntryFixed>(span)` for zero-copy deserialisation.  
> **Note:** This requires the struct to be marked `[StructLayout(LayoutKind.Sequential, Pack=1)]`.

Full entry (with variable-length fields):

```csharp
public sealed class IndexEntry
{
    public IndexEntryFixed Fixed   { get; }
    public string          Path    { get; }   // UTF-8 decoded
    public ExtraField      Extra   { get; }   // parsed key=value map
}
```

---

## 5. Phase 2 — Low-Level Reader (`DariReader`)

**Goal:** Read any valid `.dar` file correctly; surface raw data to higher layers.

### Read flow

```
1. Seek to (fileLength - 15), read 15 bytes → DariFooter (validate)
2. Seek to 0, read 13 bytes → DariHeader (validate)
3. Seek to footer.IndexOffset, read sequentially:
       for i in 0..footer.FileCount:
           read 85 bytes → IndexEntryFixed
           read PathLength bytes → string (UTF-8)
           read ExtraLength bytes → ExtraField
4. Expose index entries + stream-based data block access
```

### API sketch

```csharp
public sealed class DariReader : IDisposable, IAsyncDisposable
{
    public static ValueTask<DariReader> OpenAsync(Stream stream, CancellationToken ct = default);
    public static ValueTask<DariReader> OpenAsync(string path, CancellationToken ct = default);

    public DariHeader Header { get; }
    public IReadOnlyList<IndexEntry> Entries { get; }

    // Returns the raw (still compressed/encrypted) data block for an entry
    public ValueTask<ReadOnlyMemory<byte>> ReadRawBlockAsync(IndexEntry entry, CancellationToken ct = default);

    // Resolves linked entries, decrypts, decompresses; writes to destination
    public ValueTask ExtractEntryAsync(IndexEntry entry, Stream destination,
        DariPassphrase? passphrase = null, CancellationToken ct = default);
}
```

### Performance notes

- **Use `System.IO.Pipelines.PipeReader`** when the underlying stream supports it, for reading the index sequentially without per-read allocations.
- Read the entire index in one `ReadAsync` call if `fileLength - indexOffset - 15` fits within a pooled buffer; fall back to streaming otherwise.
- Use `ArrayPool<byte>.Shared.Rent` for intermediate byte[] buffers; always return in `finally`.
- Avoid `async` state machine allocations on the hot path: prefer `ValueTask` + `ConfigureAwait(false)`.

---

## 6. Phase 3 — Low-Level Writer (`DariWriter`)

**Goal:** Write a valid `.dar` stream in a single pass, buffering the index in memory.

### Write flow

```
1. Write 13-byte header immediately.
2. For each file added:
   a. Compute BLAKE3 checksum of original content.
   b. Check deduplication map; if hit → record linked entry, skip data write.
   c. Compress (or store raw if output >= original).
   d. If encryption enabled → encrypt.
   e. Write data block bytes; record absolute offset.
   f. Build IndexEntry, add to in-memory list.
3. Record current position as index_offset.
4. Write all IndexEntry structs sequentially.
5. Write 15-byte footer.
```

### API sketch

```csharp
public sealed class DariWriter : IAsyncDisposable
{
    public static DariWriter Create(Stream output);
    public static DariWriter Create(string path);

    public ValueTask AddFileAsync(
        string archivePath,
        Stream content,
        FileMetadata metadata,           // mtime, uid, gid, perm
        DariPassphrase? passphrase = null,
        CancellationToken ct = default);

    public ValueTask AddFileAsync(
        string archivePath,
        ReadOnlyMemory<byte> content,
        FileMetadata metadata,
        DariPassphrase? passphrase = null,
        CancellationToken ct = default);

    // Finalises the archive: writes index + footer, flushes stream
    public ValueTask FinalizeAsync(CancellationToken ct = default);
}
```

### Performance notes

- Use **`IBufferWriter<byte>`** + `System.IO.Pipelines.PipeWriter` for zero-copy writes.
- Pre-size the in-memory index list with an estimated capacity to avoid list reallocations.
- Compress with `ZstdSharp.Compressor` using stack-allocated or pooled output buffers; compare output size to input before committing.
- **Parallel compression:** When adding multiple files, consider compressing them concurrently using `System.Threading.Channels` (producer = file reader, consumer = compressed block writer). Keep a bounded channel to limit memory pressure.

---

## 7. Phase 4 — Compression (`Compression/`)

Each compressor is a stateless service implementing:

```csharp
public interface ICompressor
{
    CompressionMethod Method { get; }

    // Returns compressed bytes, or null/empty to signal "fallback to raw"
    ValueTask<ReadOnlyMemory<byte>?> CompressAsync(
        ReadOnlyMemory<byte> input, CancellationToken ct = default);

    ValueTask DecompressAsync(
        ReadOnlyMemory<byte> input, ulong originalSize,
        IBufferWriter<byte> output, CancellationToken ct = default);
}
```

### Compressor implementations

| Class | Algorithm | Write API | Read API |
|-------|-----------|-----------|----------|
| `NoneCompressor` | raw | copy | copy |
| `BrotliCompressor` | Brotli (quality=6, lgwin=22) | `BrotliEncoder` (non-allocating struct encoder) | `BrotliDecoder` |
| `ZstandardCompressor` | Zstd (level=3) | `ZstdSharp.Compressor` | `ZstdSharp.Decompressor` |
| `LzmaCompressor` | XZ/LZMA2 (preset=9) | `SharpCompress` XZ writer | `SharpCompress` XZ reader |
| `LeptonJpegCompressor` | pass-through | store raw, `method = LeptonJpeg` flag preserved | return stored bytes as-is |

### Compressor registry

```csharp
public sealed class CompressorRegistry
{
    public static CompressorRegistry Default { get; }  // singleton with all built-ins

    public CompressionMethod SelectForExtension(ReadOnlySpan<char> extension);
    public ICompressor Get(CompressionMethod method);
    public void Register(ICompressor compressor);       // allows custom overrides
}
```

Extension-to-method mapping is implemented as a `FrozenDictionary<string, CompressionMethod>` (available in .NET 8+, built once at startup) for O(1) lookups.

### Fallback rule (§8.2)

After compression, if `output.Length >= input.Length`, the compressor returns `null`; the writer then stores the raw bytes and sets `compression_method = None`.

---

## 8. Phase 5 — Encryption (`Crypto/`)

Uses only inbox APIs: `System.Security.Cryptography.ChaCha20Poly1305` and `Blake3.NET`.

```csharp
internal static class DariEncryption
{
    // Derives a 32-byte key: blake3_derive_key("dari.v1.chacha20poly1305.key", passphrase)
    public static void DeriveKey(ReadOnlySpan<byte> passphraseUtf8, Span<byte> key32);

    // nonce = checksum[0..12]
    public static void DeriveNonce(ReadOnlySpan<byte> blake3Checksum, Span<byte> nonce12);

    // plaintext → ciphertext || tag (16 bytes appended)
    public static void Encrypt(
        ReadOnlySpan<byte> key, ReadOnlySpan<byte> nonce,
        ReadOnlySpan<byte> plaintext,
        Span<byte> ciphertextAndTag);         // must be plaintext.Length + 16

    // ciphertextAndTag → plaintext; throws AuthenticationTagMismatchException on failure
    public static void Decrypt(
        ReadOnlySpan<byte> key, ReadOnlySpan<byte> nonce,
        ReadOnlySpan<byte> ciphertextAndTag,
        Span<byte> plaintext);
}
```

```csharp
public sealed class DariPassphrase : IDisposable
{
    public DariPassphrase(string passphrase);
    public DariPassphrase(ReadOnlySpan<char> passphrase);

    // Writes key material to caller-supplied span; clears internal buffer on Dispose
    internal void DeriveKey(Span<byte> key32);

    public void Dispose();   // zeroes the internal byte[] using CryptographicOperations.ZeroMemory
}
```

### Passphrase verification on append

When opening an encrypted archive for appending, trial-decrypt the first encrypted entry's data block. If `ChaCha20Poly1305.Decrypt` throws, report passphrase mismatch.

---

## 9. Phase 6 — Deduplication (`Deduplication/`)

```csharp
internal sealed class DeduplicationTracker
{
    // Registers a primary entry. Returns true if this checksum is new (not a duplicate).
    public bool TryRegisterPrimary(Blake3Hash checksum, ulong dataOffset);

    // Checks for an existing primary entry with the same checksum.
    // Returns true and the original offset if a duplicate is detected.
    public bool TryGetExisting(Blake3Hash checksum, out ulong existingOffset);
}
```

Backed by a `Dictionary<Blake3Hash, ulong>` (where `Blake3Hash` is a 32-byte value type implementing `IEquatable<Blake3Hash>` and `GetHashCode()` using the first 8 bytes reinterpreted as `long`).

When a duplicate is detected:
- The new index entry is written with `bitflags |= IndexFlags.LinkedData` and `offset = existingOffset`.
- No data bytes are written to the data section.

---

## 10. Phase 7 — Extra Fields (`Extra/`)

```csharp
public readonly struct ExtraField
{
    public static ExtraField Empty { get; }

    public static ExtraField Parse(ReadOnlySpan<char> raw);     // split on ';', unescape '%3B'
    public static ExtraField Parse(ReadOnlySpan<byte> utf8Raw); // zero-copy UTF-8 path

    public bool TryGetValue(string key, out string value);
    public string? GetValueOrDefault(string key);

    // Builder-style mutation (returns new struct)
    public ExtraField With(string key, string value);
    public ExtraField Without(string key);

    // Serialises back to "k1=v1;k2=v2", escaping any ';' as '%3B'
    public int WriteTo(Span<char> destination);
    public string Serialize();
}
```

`ExtraField` stores pairs internally as a `(string Key, string Value)[]` small array (typically 0–5 pairs), avoiding `Dictionary` allocation for the common case.

---

## 11. Phase 8 — High-Level API (`Archiving/`)

### ArchiveReader

```csharp
public sealed class ArchiveReader : IDisposable, IAsyncDisposable
{
    public static ArchiveReader Open(string path, DariPassphrase? passphrase = null);
    public static ValueTask<ArchiveReader> OpenAsync(string path,
        DariPassphrase? passphrase = null, CancellationToken ct = default);

    public DateTimeOffset CreatedAt { get; }
    public IReadOnlyList<IndexEntry> Entries { get; }

    // Extract a single entry to a stream
    public ValueTask ExtractAsync(IndexEntry entry, Stream destination,
        bool verifyChecksum = true, CancellationToken ct = default);

    // Extract a single entry to a file path
    public ValueTask ExtractToFileAsync(IndexEntry entry, string outputPath,
        bool verifyChecksum = true, CancellationToken ct = default);

    // Extract all entries to a directory
    public ValueTask ExtractAllAsync(string outputDirectory,
        bool verifyChecksums = true, CancellationToken ct = default);

    // Stream raw (already compressed) bytes, useful for repackaging
    public ValueTask<Stream> OpenRawStreamAsync(IndexEntry entry, CancellationToken ct = default);
}
```

### ArchiveWriter (fluent builder)

```csharp
public sealed class ArchiveWriter : IAsyncDisposable
{
    public static ArchiveWriter Create(string outputPath,
        DariPassphrase? passphrase = null,
        CompressorRegistry? compressors = null);

    public static ArchiveWriter Create(Stream output,
        DariPassphrase? passphrase = null,
        CompressorRegistry? compressors = null);

    // Add a single file by path; extension determines compressor
    public ValueTask AddAsync(string sourcePath, string archivePath,
        CancellationToken ct = default);

    // Add a directory tree; respects .darignore/.gitignore patterns
    public ValueTask AddDirectoryAsync(string sourceDirectory,
        string archivePrefix = "",
        IIgnoreFilter? ignoreFilter = null,
        CancellationToken ct = default);

    // Add from stream
    public ValueTask AddAsync(Stream content, string archivePath,
        FileMetadata metadata, string extension = "",
        CancellationToken ct = default);

    public ValueTask DisposeAsync();  // finalises (writes index + footer)
}
```

### ArchiveAppender

```csharp
public sealed class ArchiveAppender : IAsyncDisposable
{
    // Opens an existing .dar, reads its index & dedup map, returns a writer that
    // appends new data blocks and rewrites the index+footer atomically.
    public static ValueTask<ArchiveAppender> OpenAsync(string path,
        DariPassphrase? passphrase = null, CancellationToken ct = default);

    public IReadOnlyList<IndexEntry> ExistingEntries { get; }

    public ValueTask AddAsync(string sourcePath, string archivePath,
        CancellationToken ct = default);

    public ValueTask DisposeAsync();  // commits the updated archive
}
```

**Atomic append strategy:** Write new data blocks to a temporary file alongside the original; on `FinalizeAsync`, rename-swap so that partial writes never corrupt the existing archive.

---

## 12. Phase 9 — Tests

Create `Dari.Archiver.Tests` (xUnit + FluentAssertions):

| Test class | Coverage |
|------------|----------|
| `DariHeaderTests` | Read/write round-trip; bad magic → exception; bad version → exception |
| `DariFooterTests` | Read/write round-trip; all validation failure paths |
| `IndexEntryTests` | Fixed struct round-trip via `MemoryMarshal`; variable-length path/extra |
| `ExtraFieldTests` | Parse/serialize; semicolon escaping/unescaping; empty; duplicate key last-write-wins |
| `CompressionTests` | Each compressor: compress→decompress identity; fallback when output >= input |
| `EncryptionTests` | Encrypt→decrypt round-trip; wrong passphrase throws; nonce derivation |
| `DeduplicationTests` | Duplicate files stored once; linked flag set; extract both produce same content |
| `ArchiveRoundTripTests` | Create archive, read back all entries, verify checksums, compare content |
| `AppendTests` | Append to existing archive; dedup across existing+new entries |
| `FormatValidationTests` | Truncated files, bad offsets, reserved bits — all raise `DariFormatException` |
| `LargeFileTests` | Files > 4 GB (verifies u64 offset/size fields are handled correctly) |

---

## 13. Performance Guidelines

| Technique | Where Applied |
|-----------|---------------|
| `Span<T>` / `ReadOnlySpan<T>` | All binary parsing; zero heap allocation for struct reads |
| `MemoryMarshal.Read<T>` | Reading `IndexEntryFixed` from a buffer slice |
| `BinaryPrimitives` | All LE integer reads/writes |
| `ArrayPool<byte>.Shared` | Compression output buffers, index serialisation buffers |
| `RecyclableMemoryStream` | Intermediate in-memory streams (avoids LOH pressure) |
| `IBufferWriter<byte>` | All write paths; avoids intermediate copies |
| `PipeReader` / `PipeWriter` | Stream reading when the underlying transport supports it |
| `FrozenDictionary` | Extension → compression method lookup (built once, lock-free reads) |
| `ValueTask` everywhere | Avoids `Task` object allocation on the synchronous fast path |
| `stackalloc` | Small fixed-size buffers (header 13 B, footer 15 B, nonce 12 B, key 32 B) |
| Parallel channels | File compression during multi-file archive creation |
| `CryptographicOperations.ZeroMemory` | Wipe key material from managed memory on dispose |

---

## 14. Project File Configuration

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>   <!-- for MemoryMarshal usage -->
    <LangVersion>preview</LangVersion>
    <Optimize>true</Optimize>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Blake3.NET" Version="*" />
    <PackageReference Include="ZstdSharp.Port" Version="*" />
    <PackageReference Include="SharpCompress" Version="*" />
    <PackageReference Include="Microsoft.IO.RecyclableMemoryStream" Version="*" />
  </ItemGroup>
</Project>
```

---

## 15. Implementation Order

```
Phase 1  →  Format primitives (DariConstants, DariHeader, DariFooter, IndexEntryFixed)
Phase 2  →  ExtraField parser
Phase 3  →  DariReader (read-only, no decompression/decryption)
Phase 4  →  Compression (all four algorithms + registry)
Phase 5  →  DariWriter (no encryption, no deduplication)
Phase 6  →  ArchiveReader + ArchiveWriter high-level API
Phase 7  →  Encryption (DariEncryption, DariPassphrase)
Phase 8  →  Deduplication (DeduplicationTracker)
Phase 9  →  ArchiveAppender
Phase 10 →  Tests covering all phases
```

Each phase is independently compilable and testable before moving on.

---

## 16. Format Version Compatibility

The library targets **Dari v5** exclusively. If `header.version != 5`, `DariFormatException` is thrown immediately with a clear message indicating the unsupported version. A future migration path can be introduced via a `IDariVersionUpgrader` interface without breaking the core reader.
