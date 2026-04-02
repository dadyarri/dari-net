using System.Buffers;
using System.Runtime.InteropServices;
using System.Text;
using Dari.Archiver.Compression;
using Dari.Archiver.Crypto;
using Dari.Archiver.Deduplication;
using Dari.Archiver.Extra;
using Dari.Archiver.Format;

namespace Dari.Archiver.IO;

/// <summary>
/// Low-level writer that produces a valid <c>.dar</c> archive stream.
/// </summary>
/// <remarks>
/// <para>
/// Write flow:
/// <list type="number">
///   <item>The 13-byte header is written immediately on construction.</item>
///   <item>Each call to <see cref="AddFileAsync(string,ReadOnlyMemory{byte},FileMetadata,CompressorRegistry,ExtraField,CancellationToken)"/>
///         computes a BLAKE3 checksum, compresses (with fallback to raw), writes the data
///         block, and records an <see cref="IndexEntry"/> in memory.</item>
///   <item><see cref="FinalizeAsync"/> writes all index entries then the 15-byte footer
///         and flushes the stream.</item>
/// </list>
/// </para>
/// <para>
/// The output stream must be writable and seekable (seekability is required to back-fill
/// the index offset in the footer).  When constructed via
/// <see cref="CreateAsync(string,DariHeader?,DariPassphrase?,CancellationToken)"/> the file stream is owned
/// and disposed together with the writer.
/// </para>
/// </remarks>
public sealed class DariWriter : IAsyncDisposable, IDisposable
{
    private readonly Stream _stream;
    private readonly bool _leaveOpen;
    private readonly List<IndexEntry> _entries;
    private readonly DariPassphrase? _passphrase;
    private readonly DeduplicationTracker _dedup;
    private bool _finalized;
    private bool _disposed;

    private DariWriter(Stream stream, bool leaveOpen, DariPassphrase? passphrase, DeduplicationTracker? dedup)
    {
        _stream = stream;
        _leaveOpen = leaveOpen;
        _entries = new List<IndexEntry>(64);
        _passphrase = passphrase;
        _dedup = dedup ?? new DeduplicationTracker();
    }

    // -----------------------------------------------------------------------
    // Factory methods
    // -----------------------------------------------------------------------

    /// <summary>
    /// Creates a new <see cref="DariWriter"/> that writes to <paramref name="stream"/>.
    /// The header is written immediately.
    /// </summary>
    /// <param name="stream">A writable, seekable stream.</param>
    /// <param name="header">
    ///   Header to write, or <see langword="null"/> to use <see cref="DariHeader.CreateNew()"/>.
    /// </param>
    /// <param name="leaveOpen">When <see langword="true"/>, the stream is not disposed with the writer.</param>
    /// <param name="passphrase">When non-null, all data blocks are encrypted with ChaCha20-Poly1305.</param>
    /// <param name="ct">Cancellation token.</param>
    public static async ValueTask<DariWriter> CreateAsync(
        Stream stream,
        DariHeader? header = null,
        bool leaveOpen = false,
        DariPassphrase? passphrase = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanWrite) throw new ArgumentException("Stream must be writable.", nameof(stream));
        if (!stream.CanSeek) throw new ArgumentException("Stream must be seekable.", nameof(stream));

        var writer = new DariWriter(stream, leaveOpen, passphrase, dedup: null);
        await writer.WriteHeaderAsync(header ?? DariHeader.CreateNew(), ct).ConfigureAwait(false);
        return writer;
    }

    /// <summary>
    /// Creates a new <see cref="DariWriter"/> that writes to a file at <paramref name="path"/>.
    /// The file is created (or truncated if it exists).  The writer owns the file stream.
    /// </summary>
    /// <param name="path">Output file path.</param>
    /// <param name="header">Header to write; defaults to a new header with the current timestamp.</param>
    /// <param name="passphrase">When non-null, all data blocks are encrypted with ChaCha20-Poly1305.</param>
    /// <param name="ct">Cancellation token.</param>
    public static ValueTask<DariWriter> CreateAsync(
        string path,
        DariHeader? header = null,
        DariPassphrase? passphrase = null,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fs = new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.None,
                                bufferSize: 65536, useAsync: true);
        return CreateAsync(fs, header, leaveOpen: false, passphrase, ct);
    }

    /// <summary>
    /// Internal factory used by <see cref="Archiving.ArchiveAppender"/> to resume writing
    /// into an existing archive stream (no header is written; the stream must already be
    /// positioned at the end of the data section).
    /// </summary>
    internal static DariWriter Resume(
        Stream stream,
        bool leaveOpen,
        DariPassphrase? passphrase,
        DeduplicationTracker dedup)
    {
        ArgumentNullException.ThrowIfNull(stream);
        return new DariWriter(stream, leaveOpen, passphrase, dedup);
    }

    /// <summary>
    /// Injects pre-existing <see cref="IndexEntry"/> records at the <em>front</em> of the
    /// internal entry list so that they appear first in the written index.
    /// Used by <see cref="Archiving.ArchiveAppender"/> to preserve existing entries.
    /// </summary>
    internal void InjectEntries(IReadOnlyList<IndexEntry> entries)
    {
        _entries.InsertRange(0, entries);
    }

    // -----------------------------------------------------------------------
    // Writing files
    // -----------------------------------------------------------------------

    /// <summary>
    /// Reads <paramref name="content"/> to end, compresses it (using
    /// <paramref name="registry"/> or <see cref="CompressorRegistry.Default"/>),
    /// writes the data block to the archive, and records an index entry.
    /// </summary>
    /// <param name="archivePath">Forward-slash-separated path stored in the archive index.</param>
    /// <param name="content">Stream to read; does not need to be seekable.</param>
    /// <param name="metadata">File metadata (mtime, uid, gid, perm).</param>
    /// <param name="registry">Compressor registry; defaults to <see cref="CompressorRegistry.Default"/>.</param>
    /// <param name="extra">Optional extra fields to store in the index entry.</param>
    /// <param name="ct">Cancellation token.</param>
    public async ValueTask AddFileAsync(
        string archivePath,
        Stream content,
        FileMetadata metadata,
        CompressorRegistry? registry = null,
        ExtraField extra = default,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(archivePath);
        ArgumentNullException.ThrowIfNull(content);
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_finalized) throw new InvalidOperationException("Archive has already been finalized.");

        byte[] rawBytes = await ReadAllBytesAsync(content, ct).ConfigureAwait(false);
        await AddFileCoreAsync(archivePath, rawBytes, metadata, registry, extra, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Compresses <paramref name="content"/>, writes the data block to the archive,
    /// and records an index entry.
    /// </summary>
    /// <param name="archivePath">Forward-slash-separated path stored in the archive index.</param>
    /// <param name="content">Raw file bytes.</param>
    /// <param name="metadata">File metadata (mtime, uid, gid, perm).</param>
    /// <param name="registry">Compressor registry; defaults to <see cref="CompressorRegistry.Default"/>.</param>
    /// <param name="extra">Optional extra fields to store in the index entry.</param>
    /// <param name="ct">Cancellation token.</param>
    public ValueTask AddFileAsync(
        string archivePath,
        ReadOnlyMemory<byte> content,
        FileMetadata metadata,
        CompressorRegistry? registry = null,
        ExtraField extra = default,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(archivePath);
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_finalized) throw new InvalidOperationException("Archive has already been finalized.");

        return AddFileCoreAsync(archivePath, content.ToArray(), metadata, registry, extra, ct);
    }

    private async ValueTask AddFileCoreAsync(
        string archivePath,
        byte[] rawBytes,
        FileMetadata metadata,
        CompressorRegistry? registry,
        ExtraField extra,
        CancellationToken ct)
    {
        // 1. Compute BLAKE3 checksum of the original content.
        Blake3Hash checksum = ComputeBlake3(rawBytes);

        // 2. Deduplication check: if we have seen this content before, emit a linked entry.
        if (_dedup.TryGetExisting(checksum, out ulong existingOffset, out CompressionMethod primaryMethod))
        {
            var linkedEntry = BuildEntry(
                archivePath, extra, checksum, metadata,
                dataOffset: existingOffset,
                method: primaryMethod,
                originalSize: (ulong)rawBytes.Length,
                compressedSize: 0,
                flags: IndexFlags.LinkedData);
            _entries.Add(linkedEntry);
            return;
        }

        // 3. Compress (with fallback to raw if output >= input).
        var reg = registry ?? CompressorRegistry.Default;
        string extension = System.IO.Path.GetExtension(archivePath);
        CompressionMethod method = reg.SelectForExtension(extension.AsSpan());
        ICompressor compressor = reg.Get(method);

        ReadOnlyMemory<byte> storedBytes;
        CompressionMethod storedMethod;

        ReadOnlyMemory<byte>? compressed =
            await compressor.CompressAsync(rawBytes, ct).ConfigureAwait(false);

        if (compressed is null)
        {
            storedBytes = rawBytes;
            storedMethod = CompressionMethod.None;
        }
        else
        {
            storedBytes = compressed.Value;
            storedMethod = method;
        }

        // 4. Encrypt (if passphrase provided): compress → encrypt.
        IndexFlags flags = IndexFlags.None;
        if (_passphrase is not null)
        {
            Span<byte> key = stackalloc byte[DariConstants.KeySize];
            _passphrase.DeriveKey(key);

            // Nonce = first 12 bytes of the BLAKE3 content checksum (§9.2).
            Span<byte> nonce = stackalloc byte[DariConstants.NonceSize];
            Span<byte> checksumBytes = stackalloc byte[32];
            checksum.CopyTo(checksumBytes);
            DariEncryption.DeriveNonce(checksumBytes, nonce);

            byte[] ciphertextAndTag = new byte[storedBytes.Length + DariConstants.TagSize];
            DariEncryption.Encrypt(key, nonce, storedBytes.Span, ciphertextAndTag);

            // Store the nonce and tag hex in extra fields.
            string nonceHex = Convert.ToHexStringLower(nonce);
            string tagHex = Convert.ToHexStringLower(ciphertextAndTag[^DariConstants.TagSize..]);
            extra = extra
                .With(WellKnownExtraKeys.EncryptionAlgorithm, "chacha20poly1305")
                .With(WellKnownExtraKeys.EncryptionNonce, nonceHex)
                .With(WellKnownExtraKeys.EncryptionTag, tagHex);

            storedBytes = ciphertextAndTag;
            flags = IndexFlags.EncryptedData;
        }

        // 5. Record the current stream position and register as primary in the dedup map.
        ulong dataOffset = (ulong)_stream.Position;
        _dedup.TryRegisterPrimary(checksum, dataOffset, storedMethod);

        // 6. Write the data block.
        if (storedBytes.Length > 0)
            await _stream.WriteAsync(storedBytes, ct).ConfigureAwait(false);

        // 7. Build and record the IndexEntry.
        var entry = BuildEntry(
            archivePath, extra, checksum, metadata,
            dataOffset, storedMethod,
            originalSize: (ulong)rawBytes.Length,
            compressedSize: (ulong)storedBytes.Length,
            flags: flags);

        _entries.Add(entry);
    }

    // -----------------------------------------------------------------------
    // Finalize
    // -----------------------------------------------------------------------

    /// <summary>
    /// Writes all accumulated index entries followed by the 15-byte footer, then flushes.
    /// After this call no more files can be added.
    /// </summary>
    public async ValueTask FinalizeAsync(CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_finalized) throw new InvalidOperationException("Archive has already been finalized.");
        _finalized = true;

        // Record where the index begins.
        uint indexOffset = (uint)_stream.Position;

        // Write each index entry.
        foreach (var entry in _entries)
        {
            byte[] pathBytes = Encoding.UTF8.GetBytes(entry.Path);
            byte[] extraBytes = Encoding.UTF8.GetBytes(entry.Extra.Serialize());
            int entrySize = DariConstants.IndexEntryFixedSize + pathBytes.Length + extraBytes.Length;

            byte[] buf = ArrayPool<byte>.Shared.Rent(entrySize);
            try
            {
                int written = entry.WriteTo(buf.AsSpan(0, entrySize));
                await _stream.WriteAsync(buf.AsMemory(0, written), ct).ConfigureAwait(false);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buf);
            }
        }

        // Write the 15-byte footer.
        byte[] footerBuf = ArrayPool<byte>.Shared.Rent(DariConstants.FooterSize);
        try
        {
            var footer = DariFooter.Create(indexOffset, (uint)_entries.Count);
            footer.WriteTo(footerBuf.AsSpan(0, DariConstants.FooterSize));
            await _stream.WriteAsync(footerBuf.AsMemory(0, DariConstants.FooterSize), ct).ConfigureAwait(false);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(footerBuf);
        }

        await _stream.FlushAsync(ct).ConfigureAwait(false);
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private async ValueTask WriteHeaderAsync(DariHeader header, CancellationToken ct)
    {
        byte[] buf = ArrayPool<byte>.Shared.Rent(DariConstants.HeaderSize);
        try
        {
            header.WriteTo(buf.AsSpan(0, DariConstants.HeaderSize));
            await _stream.WriteAsync(buf.AsMemory(0, DariConstants.HeaderSize), ct).ConfigureAwait(false);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buf);
        }
    }

    private static async Task<byte[]> ReadAllBytesAsync(Stream stream, CancellationToken ct)
    {
        if (stream.CanSeek)
        {
            long remaining = stream.Length - stream.Position;
            if (remaining >= 0 && remaining <= int.MaxValue)
            {
                byte[] buf = new byte[(int)remaining];
                await BinaryHelpers.ReadExactFromAsync(stream, buf, ct).ConfigureAwait(false);
                return buf;
            }
        }

        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, ct).ConfigureAwait(false);
        return ms.ToArray();
    }

    private static Blake3Hash ComputeBlake3(ReadOnlySpan<byte> data) => Blake3Hash.Of(data);

    private static IndexEntry BuildEntry(
        string archivePath,
        ExtraField extra,
        Blake3Hash checksum,
        FileMetadata metadata,
        ulong dataOffset,
        CompressionMethod method,
        ulong originalSize,
        ulong compressedSize,
        IndexFlags flags)
    {
        byte[] pathBytes = Encoding.UTF8.GetBytes(archivePath);
        byte[] extraBytes = Encoding.UTF8.GetBytes(extra.Serialize());

        // Build the fixed struct via MemoryMarshal to match [StructLayout Pack=1] exactly.
        Span<byte> fixedBuf = stackalloc byte[DariConstants.IndexEntryFixedSize];
        fixedBuf.Clear();

        WriteUInt64LE(fixedBuf, 0, dataOffset);
        WriteUInt16LE(fixedBuf, 8, (ushort)flags);
        fixedBuf[10] = (byte)method;
        WriteUInt64LE(fixedBuf, 11, (ulong)metadata.ModifiedAt.ToUnixTimeSeconds());
        WriteUInt32LE(fixedBuf, 19, metadata.Uid);
        WriteUInt32LE(fixedBuf, 23, metadata.Gid);
        WriteUInt16LE(fixedBuf, 27, metadata.Perm);
        checksum.CopyTo(fixedBuf[29..]);   // 32 bytes at offset 29
        WriteUInt64LE(fixedBuf, 61, originalSize);
        WriteUInt64LE(fixedBuf, 69, compressedSize);
        WriteUInt32LE(fixedBuf, 77, (uint)pathBytes.Length);
        WriteUInt32LE(fixedBuf, 81, (uint)extraBytes.Length);

        var fixedPart = MemoryMarshal.Read<IndexEntryFixed>(fixedBuf);
        return new IndexEntry(fixedPart, archivePath, extra);
    }

    private static void WriteUInt64LE(Span<byte> buf, int offset, ulong value) =>
        System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(buf[offset..], value);

    private static void WriteUInt32LE(Span<byte> buf, int offset, uint value) =>
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(buf[offset..], value);

    private static void WriteUInt16LE(Span<byte> buf, int offset, ushort value) =>
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(buf[offset..], value);

    // -----------------------------------------------------------------------
    // Dispose
    // -----------------------------------------------------------------------

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (!_leaveOpen) _stream.Dispose();
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        if (!_finalized)
        {
            try { await FinalizeAsync().ConfigureAwait(false); }
            catch { /* best-effort */ }
        }
        _disposed = true;
        if (!_leaveOpen) await _stream.DisposeAsync().ConfigureAwait(false);
    }
}
