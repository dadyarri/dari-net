using System.Buffers;
using System.Security.Cryptography;
using Dari.Archiver.Compression;
using Dari.Archiver.Crypto;
using Dari.Archiver.Diagnostics;
using Dari.Archiver.Extra;
using Dari.Archiver.Format;
using Dari.Archiver.IO;

namespace Dari.Archiver.Archiving;

/// <summary>
/// High-level archive reader: opens a <c>.dar</c> file, decompresses entries on extraction,
/// and optionally verifies BLAKE3 checksums.
/// </summary>
/// <remarks>
/// Built on top of the low-level <see cref="DariReader"/>. Encryption is not yet supported
/// in this release; passing a non-null passphrase will throw <see cref="NotSupportedException"/>.
/// </remarks>
public sealed class ArchiveReader : IDisposable, IAsyncDisposable
{
    private readonly DariReader _inner;
    private readonly CompressorRegistry _registry;
    private readonly DariPassphrase? _passphrase;

    /// <summary>UTC timestamp written into the archive header.</summary>
    public DateTimeOffset CreatedAt => _inner.Header.CreatedAt;

    /// <summary>All index entries, in the order stored in the archive.</summary>
    public IReadOnlyList<IndexEntry> Entries => _inner.Entries;

    private ArchiveReader(DariReader inner, CompressorRegistry registry, DariPassphrase? passphrase)
    {
        _inner = inner;
        _registry = registry;
        _passphrase = passphrase;
    }

    // -----------------------------------------------------------------------
    // Factory methods
    // -----------------------------------------------------------------------

    /// <summary>
    /// Opens and parses a <c>.dar</c> archive at <paramref name="path"/>.
    /// </summary>
    /// <param name="path">Path to the <c>.dar</c> file.</param>
    /// <param name="compressors">
    ///   Compressor registry to use for decompression.
    ///   Defaults to <see cref="CompressorRegistry.Default"/>.
    /// </param>
    /// <param name="passphrase">Passphrase for decrypting encrypted entries; <see langword="null"/> for plain archives.</param>
    /// <param name="ct">Cancellation token.</param>
    public static async ValueTask<ArchiveReader> OpenAsync(
        string path,
        CompressorRegistry? compressors = null,
        DariPassphrase? passphrase = null,
        CancellationToken ct = default)
    {
        var inner = await DariReader.OpenAsync(path, ct).ConfigureAwait(false);
        return new ArchiveReader(inner, compressors ?? CompressorRegistry.Default, passphrase);
    }

    /// <summary>
    /// Opens and parses a <c>.dar</c> archive from <paramref name="stream"/>.
    /// </summary>
    /// <param name="stream">A seekable, readable stream.</param>
    /// <param name="leaveOpen">When <see langword="true"/> the stream is not disposed with the reader.</param>
    /// <param name="compressors">Compressor registry; defaults to <see cref="CompressorRegistry.Default"/>.</param>
    /// <param name="passphrase">Passphrase for decrypting encrypted entries; <see langword="null"/> for plain archives.</param>
    /// <param name="ct">Cancellation token.</param>
    public static async ValueTask<ArchiveReader> OpenAsync(
        Stream stream,
        bool leaveOpen = false,
        CompressorRegistry? compressors = null,
        DariPassphrase? passphrase = null,
        CancellationToken ct = default)
    {
        var inner = await DariReader.OpenAsync(stream, leaveOpen, ct).ConfigureAwait(false);
        return new ArchiveReader(inner, compressors ?? CompressorRegistry.Default, passphrase);
    }

    // -----------------------------------------------------------------------
    // Extraction
    // -----------------------------------------------------------------------

    /// <summary>
    /// Decompresses <paramref name="entry"/> and writes the raw content to <paramref name="destination"/>.
    /// </summary>
    /// <param name="entry">The entry to extract.</param>
    /// <param name="destination">Writable stream that receives the decompressed bytes.</param>
    /// <param name="verifyChecksum">
    ///   When <see langword="true"/> (the default), the BLAKE3 checksum of the decompressed
    ///   content is compared with the value stored in the index entry.
    ///   Throws <see cref="InvalidDataException"/> on mismatch.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    public async ValueTask ExtractAsync(
        IndexEntry entry,
        Stream destination,
        bool verifyChecksum = true,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(destination);

        var rawBlock = await _inner.ReadRawBlockAsync(entry, ct).ConfigureAwait(false);
        var decompressed = await DecompressAsync(entry, rawBlock, ct).ConfigureAwait(false);

        if (verifyChecksum)
            VerifyChecksum(entry, decompressed.Span);

        await destination.WriteAsync(decompressed, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Decompresses <paramref name="entry"/> and writes the content to a file at <paramref name="outputPath"/>.
    /// Parent directories are created as needed.
    /// </summary>
    public async ValueTask ExtractToFileAsync(
        IndexEntry entry,
        string outputPath,
        bool verifyChecksum = true,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");

        await using var fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write,
                                            FileShare.None, bufferSize: 65536, useAsync: true);
        await ExtractAsync(entry, fs, verifyChecksum, ct).ConfigureAwait(false);

        // Restore mtime when possible.
        File.SetLastWriteTimeUtc(outputPath, entry.ModifiedAt.UtcDateTime);
    }

    /// <summary>
    /// Extracts all entries to <paramref name="outputDirectory"/>, recreating the directory tree.
    /// </summary>
    /// <param name="outputDirectory">Root directory to extract into.</param>
    /// <param name="verifyChecksums">Verify BLAKE3 checksum for every extracted file.</param>
    /// <param name="ct">Cancellation token.</param>
    public async ValueTask ExtractAllAsync(
        string outputDirectory,
        bool verifyChecksums = true,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);

        foreach (var entry in Entries)
        {
            ct.ThrowIfCancellationRequested();

            // Forward-slash paths are always relative — join safely regardless of OS.
            string relPath = entry.Path.Replace('/', Path.DirectorySeparatorChar);
            string fullPath = Path.Combine(outputDirectory, relPath);

            await ExtractToFileAsync(entry, fullPath, verifyChecksums, ct).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Returns a <see cref="ReadOnlyMemory{T}"/> of the raw (still compressed) data block
    /// for <paramref name="entry"/>. Useful for repackaging without decompression.
    /// </summary>
    public ValueTask<ReadOnlyMemory<byte>> OpenRawBlockAsync(
        IndexEntry entry, CancellationToken ct = default) =>
        _inner.ReadRawBlockAsync(entry, ct);

    /// <summary>
    /// Reads and decompresses up to <paramref name="maxBytes"/> of <paramref name="entry"/>'s
    /// content without writing it anywhere — intended for in-pane preview.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the entry is encrypted.</exception>
    public async ValueTask<ReadOnlyMemory<byte>> ReadDecompressedPreviewAsync(
        IndexEntry entry, int maxBytes = 1 << 20, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        if (entry.IsEncrypted)
            throw new InvalidOperationException("Entry is encrypted");

        var rawBlock = await _inner.ReadRawBlockAsync(entry, ct).ConfigureAwait(false);

        if (entry.Compression == CompressionMethod.None)
            return rawBlock.Length <= maxBytes ? rawBlock : rawBlock[..maxBytes];

        var writer = new ArrayBufferWriter<byte>((int)entry.OriginalSize);
        var compressor = _registry.Get(entry.Compression);
        await compressor.DecompressAsync(rawBlock, entry.OriginalSize, writer, ct)
                        .ConfigureAwait(false);

        var decompressed = writer.WrittenMemory;
        return decompressed.Length <= maxBytes ? decompressed : decompressed[..maxBytes];
    }

    /// <summary>
    /// Verifies <paramref name="passphrase"/> by attempting to decrypt the first encrypted entry.
    /// Returns <see langword="true"/> if correct (or if the archive has no encrypted entries),
    /// <see langword="false"/> if the passphrase is wrong.
    /// </summary>
    public async ValueTask<bool> VerifyPassphraseAsync(
        DariPassphrase passphrase, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(passphrase);

        var probe = Entries.FirstOrDefault(e => e.IsEncrypted && !e.IsLinked);
        if (probe is null) return true;

        var rawBlock = await _inner.ReadRawBlockAsync(probe, ct).ConfigureAwait(false);

        string? nonceHex = probe.Extra.GetValueOrDefault(WellKnownExtraKeys.EncryptionNonce);
        if (nonceHex is null) return false;

        int plaintextLen = rawBlock.Length - DariConstants.TagSize;
        if (plaintextLen < 0) return false;

        Span<byte> key = stackalloc byte[DariConstants.KeySize];
        passphrase.DeriveKey(key);

        byte[] plaintext = ArrayPool<byte>.Shared.Rent(plaintextLen);
        try
        {
            DariEncryption.Decrypt(key, Convert.FromHexString(nonceHex), rawBlock.Span,
                                   plaintext.AsSpan(0, plaintextLen));
            return true;
        }
        catch (AuthenticationTagMismatchException)
        {
            return false;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(plaintext);
        }
    }

    // -----------------------------------------------------------------------
    // Internal helpers
    // -----------------------------------------------------------------------

    private async ValueTask<ReadOnlyMemory<byte>> DecompressAsync(
        IndexEntry entry, ReadOnlyMemory<byte> rawBlock, CancellationToken ct)
    {
        // Order: decrypt → decompress (inverse of write order: compress → encrypt).
        var block = rawBlock;

        if (entry.IsEncrypted)
        {
            if (_passphrase is null)
                throw new InvalidOperationException(
                    $"Entry '{entry.Path}' is encrypted but no passphrase was provided.");

            block = DecryptBlock(entry, block);
        }

        if (entry.Compression == CompressionMethod.None)
            return block;

        var writer = new ArrayBufferWriter<byte>((int)entry.OriginalSize);
        var compressor = _registry.Get(entry.Compression);
        await compressor.DecompressAsync(block, entry.OriginalSize, writer, ct)
                        .ConfigureAwait(false);
        return writer.WrittenMemory;
    }

    private ReadOnlyMemory<byte> DecryptBlock(IndexEntry entry, ReadOnlyMemory<byte> ciphertextAndTag)
    {
        // Nonce is stored in extra field "en" as lowercase hex (24 chars = 12 bytes).
        string? nonceHex = entry.Extra.GetValueOrDefault(WellKnownExtraKeys.EncryptionNonce);
        if (nonceHex is null)
            throw new InvalidDataException(
                $"Entry '{entry.Path}' is marked encrypted but has no nonce in extra fields.");

        byte[] nonce = Convert.FromHexString(nonceHex);

        int plaintextLen = ciphertextAndTag.Length - DariConstants.TagSize;
        if (plaintextLen < 0)
            throw new InvalidDataException(
                $"Encrypted block for '{entry.Path}' is too short to contain an auth tag.");

        Span<byte> key = stackalloc byte[DariConstants.KeySize];
        _passphrase!.DeriveKey(key);

        byte[] plaintext = new byte[plaintextLen];
        try
        {
            DariEncryption.Decrypt(key, nonce, ciphertextAndTag.Span, plaintext);
        }
        catch (AuthenticationTagMismatchException ex)
        {
            throw DariFormatException.WrongPassphrase(entry.Path, ex);
        }
        return plaintext;
    }

    private static void VerifyChecksum(IndexEntry entry, ReadOnlySpan<byte> data)
    {
        var computed = Blake3Hash.Of(data);
        if (computed != entry.Checksum)
            throw new InvalidDataException(
                $"Checksum mismatch for '{entry.Path}': " +
                $"expected {entry.Checksum}, got {computed}.");
    }

    // -----------------------------------------------------------------------
    // Dispose
    // -----------------------------------------------------------------------

    /// <inheritdoc/>
    public void Dispose() => _inner.Dispose();

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => _inner.DisposeAsync();
}
