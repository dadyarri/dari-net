# Dari.Archiver — Agent Guide

## Project Overview
`Dari.Archiver` is a .NET 10 / C# 13 class library implementing read/write support for the **Dari v5** binary archive format. No CLI tooling lives here — that is a separate project. The test project is `Dari.Archiver.Tests` (xUnit).

## Build & Test
```sh
dotnet build          # build everything (TreatWarningsAsErrors=true)
dotnet test           # run all xUnit tests
dotnet format         # run after completing any task to enforce code style
```
SDK version is pinned to `10.0.x` via `global.json`. `LangVersion=preview` is set, so all C# 13/preview features are available.

## Architecture
Layers must be implemented in this order (see `roadmap.md §15`):

| Directory | Purpose | Status |
|---|---|---|
| `Format/` | Dari binary structs, enums, constants | ✅ Done |
| `Extra/` | `ExtraField` key=value parser | ✅ Done |
| `Diagnostics/` | `DariFormatException` | ✅ Done |
| `IO/` | `DariReader`, `DariWriter`, `BinaryHelpers` | 🔲 Next |
| `Compression/` | `ICompressor` + 4 implementations + registry | 🔲 Planned |
| `Crypto/` | `DariEncryption`, `DariPassphrase` | 🔲 Planned |
| `Deduplication/` | `DeduplicationTracker` | 🔲 Planned |
| `Archiving/` | `ArchiveReader`, `ArchiveWriter`, `ArchiveAppender` | 🔲 Planned |

## Critical Format Facts (Dari v5)
- **Header**: 13 bytes at offset 0 — magic `"DARI"`, version `5`, u64 LE timestamp.
- **Footer**: last 15 bytes — magic `"DARIEND"`, u32 LE `index_offset`, u32 LE `file_count`.
- **Index entries**: fixed 85-byte struct (`IndexEntryFixed`, `Pack=1`) followed by UTF-8 path then UTF-8 extra string. Read via `MemoryMarshal.Read<IndexEntryFixed>(span)`.
- **Read order**: seek to `(fileLength - 15)` → footer → seek to 0 → header → seek to `footer.IndexOffset` → read index entries.
- Checksum is always BLAKE3 of original (uncompressed, unencrypted) bytes.
- If `compressor.output.Length >= input.Length`, store raw and set `Compression = None` (fallback rule §8.2).
- Linked (deduplicated) entries share the primary entry's `Offset`; `IndexFlags.LinkedData` is set.

## Coding Conventions
- **Format structs are `readonly struct`** with `static ReadFrom(ReadOnlySpan<byte>)` + `WriteTo(Span<byte>)` factory pattern (see `DariHeader.cs`, `DariFooter.cs`, `IndexEntry.cs`).
- **All integer I/O uses `BinaryPrimitives`** (little-endian) — never `BitConverter`.
- **`stackalloc`** for small fixed-size buffers: `stackalloc byte[DariConstants.HeaderSize]` (13 B), footer (15 B), nonce (12 B), key (32 B).
- **`ArrayPool<byte>.Shared`** for larger intermediate buffers; always return in `finally`.
- **`ValueTask`** on all async methods; `ConfigureAwait(false)` throughout.
- **`DariFormatException`** uses internal static factory methods per error kind, e.g. `DariFormatException.BadHeaderMagic()`.
- `[StructLayout(LayoutKind.Sequential, Pack=1)]` is required on any struct read via `MemoryMarshal.Read<T>`.
- `AssemblyInfo.cs` grants `InternalsVisibleTo("Dari.Archiver.Tests")` — internal types are testable directly.

## Testing Conventions
- Test classes are `public sealed class`, test methods use `[Fact]` (globally imported via `<Using Include="Xunit" />`).
- No `FluentAssertions` — use plain xUnit `Assert.*`.
- Use `stackalloc` for small byte buffers in tests (mirrors production code); use `new byte[]` for larger inputs.
- Negative tests always assert the **exception message** contains the relevant detail (e.g. `Assert.Contains("version 3", ex.Message)`).

## Key Dependencies
| Package | Use |
|---|---|
| `Blake3.NET` | BLAKE3 checksums + KDF (`"dari.v1.chacha20poly1305.key"` context) |
| `ZstdSharp.Port` | Zstandard compression (level 3) |
| `SharpCompress` | LZMA/XZ compression (preset 9, XZ container) |
| `Microsoft.IO.RecyclableMemoryStream` | Pooled `MemoryStream` to avoid LOH pressure |
| `System.Security.Cryptography.ChaCha20Poly1305` | Encryption (inbox .NET 10) |

## Encryption Details (§9)
Key = `blake3_derive_key("dari.v1.chacha20poly1305.key", passphrase_bytes)`.  
Nonce = first 12 bytes of the entry's BLAKE3 checksum (`Blake3Hash.CopyNonceTo`).  
Ciphertext layout: `ciphertext || 16-byte-tag`. `DariPassphrase.Dispose()` zeroes key material via `CryptographicOperations.ZeroMemory`.


