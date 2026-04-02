using System.Buffers;
using Dari.Archiver.Diagnostics;
using Dari.Archiver.Format;

namespace Dari.Archiver.IO;

/// <summary>
/// Reads a <c>.dar</c> archive from a <see cref="Stream"/>, parsing the header, footer,
/// and index on open. Data blocks are read on demand via <see cref="ReadRawBlockAsync"/>.
/// </summary>
/// <remarks>
/// <para>
/// This is the low-level reader: it surfaces raw (still compressed / still encrypted)
/// data block bytes. Decompression and decryption are handled by higher-level layers built
/// on top of this class.
/// </para>
/// <para>
/// The underlying stream must be seekable and readable. When opened via
/// <see cref="OpenAsync(Stream, bool, CancellationToken)"/> with <c>leaveOpen: false</c>
/// (the default) the stream is disposed together with the reader.
/// </para>
/// </remarks>
public sealed class DariReader : IDisposable, IAsyncDisposable
{
    private readonly Stream _stream;
    private readonly bool _leaveOpen;
    private bool _disposed;

    /// <summary>The parsed archive header.</summary>
    public DariHeader Header { get; }

    /// <summary>All index entries, in the order they appear in the archive index.</summary>
    public IReadOnlyList<IndexEntry> Entries { get; }

    private DariReader(Stream stream, bool leaveOpen, DariHeader header, IReadOnlyList<IndexEntry> entries)
    {
        _stream = stream;
        _leaveOpen = leaveOpen;
        Header = header;
        Entries = entries;
    }

    // -----------------------------------------------------------------------
    // Factory methods
    // -----------------------------------------------------------------------

    /// <summary>
    /// Opens and reads the header, footer, and full index from <paramref name="stream"/>.
    /// </summary>
    /// <param name="stream">A seekable, readable stream positioned anywhere.</param>
    /// <param name="leaveOpen">
    ///   When <see langword="true"/> the stream is not closed when the reader is disposed.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    public static ValueTask<DariReader> OpenAsync(
        Stream stream,
        bool leaveOpen = false,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanSeek) throw new ArgumentException("Stream must be seekable.", nameof(stream));
        if (!stream.CanRead) throw new ArgumentException("Stream must be readable.", nameof(stream));

        return OpenCoreAsync(stream, leaveOpen, ct);
    }

    /// <summary>
    /// Opens the file at <paramref name="path"/> and reads the header, footer, and full index.
    /// The underlying file stream is owned by the reader and closed on dispose.
    /// </summary>
    public static ValueTask<DariReader> OpenAsync(string path, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
                                bufferSize: 4096, useAsync: true);
        return OpenCoreAsync(fs, leaveOpen: false, ct);
    }

    // -----------------------------------------------------------------------
    // Core open logic
    // -----------------------------------------------------------------------

    private static async ValueTask<DariReader> OpenCoreAsync(
        Stream stream, bool leaveOpen, CancellationToken ct)
    {
        long fileLength = stream.Length;

        // Reject obviously invalid files before any seeking.
        if (fileLength < DariConstants.MinArchiveSize)
            throw DariFormatException.FileTooShort(fileLength);

        // ── 1. Read and validate footer (last 15 bytes) ──────────────────
        stream.Seek(-DariConstants.FooterSize, SeekOrigin.End);
        byte[] footerBuf = await BinaryHelpers.ReadExactPooledAsync(
            stream, DariConstants.FooterSize, ct).ConfigureAwait(false);
        DariFooter footer;
        try
        {
            footer = DariFooter.ReadFrom(footerBuf, fileLength);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(footerBuf);
        }

        // ── 2. Read and validate header (first 13 bytes) ──────────────────
        stream.Seek(0, SeekOrigin.Begin);
        byte[] headerBuf = await BinaryHelpers.ReadExactPooledAsync(
            stream, DariConstants.HeaderSize, ct).ConfigureAwait(false);
        DariHeader header;
        try
        {
            header = DariHeader.ReadFrom(headerBuf);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(headerBuf);
        }

        // ── 3. Read and parse index ────────────────────────────────────────
        int indexSize = (int)(fileLength - DariConstants.FooterSize - footer.IndexOffset);
        var entries = new List<IndexEntry>((int)footer.FileCount);

        if (indexSize > 0 && footer.FileCount > 0)
        {
            stream.Seek(footer.IndexOffset, SeekOrigin.Begin);
            byte[] indexBuf = await BinaryHelpers.ReadExactPooledAsync(
                stream, indexSize, ct).ConfigureAwait(false);
            try
            {
                ParseIndex(indexBuf.AsSpan(0, indexSize), footer.FileCount, entries);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(indexBuf);
            }
        }

        return new DariReader(stream, leaveOpen, header, entries.AsReadOnly());
    }

    private static void ParseIndex(ReadOnlySpan<byte> span, uint fileCount, List<IndexEntry> entries)
    {
        int pos = 0;
        for (uint i = 0; i < fileCount; i++)
        {
            var entry = IndexEntry.ReadFrom(span[pos..], out int consumed);
            entries.Add(entry);
            pos += consumed;
        }
    }

    // -----------------------------------------------------------------------
    // Data block access
    // -----------------------------------------------------------------------

    /// <summary>
    /// Reads the raw (compressed and/or encrypted) data block for <paramref name="entry"/>
    /// and returns it as a newly allocated <see cref="ReadOnlyMemory{T}"/>.
    /// </summary>
    /// <remarks>
    /// For deduplicated entries (<see cref="IndexEntry.IsLinked"/> = <see langword="true"/>)
    /// the returned bytes are those of the primary entry's data block, as per the spec.
    /// Callers at higher layers are responsible for decompression and decryption.
    /// </remarks>
    public async ValueTask<ReadOnlyMemory<byte>> ReadRawBlockAsync(
        IndexEntry entry, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(entry);

        int blockSize = (int)entry.CompressedSize;
        if (blockSize == 0)
            return ReadOnlyMemory<byte>.Empty;

        _stream.Seek((long)entry.Offset, SeekOrigin.Begin);
        return await BinaryHelpers.ReadExactAsync(_stream, blockSize, ct).ConfigureAwait(false);
    }

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
        _disposed = true;
        if (!_leaveOpen) await _stream.DisposeAsync().ConfigureAwait(false);
    }
}
