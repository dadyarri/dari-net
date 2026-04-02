using Dari.Archiver.Crypto;
using Dari.Archiver.Deduplication;
using Dari.Archiver.Format;
using Dari.Archiver.IO;

namespace Dari.Archiver.Archiving;

/// <summary>
/// Appends new files to an existing <c>.dar</c> archive atomically.
/// </summary>
/// <remarks>
/// <para>
/// The strategy is safe against partial writes:
/// <list type="number">
///   <item>Existing header + data blocks are copied verbatim to a temp file.</item>
///   <item>New data blocks are appended to the temp file.</item>
///   <item>On <see cref="FinalizeAsync"/> a fresh index and footer covering all entries
///         (existing + new) are written.</item>
///   <item>The temp file is atomically renamed over the original.</item>
/// </list>
/// A crash before the rename leaves the original intact.
/// </para>
/// </remarks>
public sealed class ArchiveAppender : IAsyncDisposable
{
    private readonly string _originalPath;
    private readonly string _tempPath;
    private readonly DariWriter _writer;
    private readonly List<IndexEntry> _existingEntries;
    private bool _finalized;
    private bool _disposed;

    /// <summary>All entries present in the archive before appending began.</summary>
    public IReadOnlyList<IndexEntry> ExistingEntries => _existingEntries;

    private ArchiveAppender(
        string originalPath, string tempPath,
        DariWriter writer, List<IndexEntry> existingEntries)
    {
        _originalPath = originalPath;
        _tempPath = tempPath;
        _writer = writer;
        _existingEntries = existingEntries;
    }

    // -----------------------------------------------------------------------
    // Factory
    // -----------------------------------------------------------------------

    /// <summary>Opens an existing <c>.dar</c> archive for appending.</summary>
    /// <param name="path">Path to the existing <c>.dar</c> file.</param>
    /// <param name="passphrase">Passphrase for encrypting new entries.</param>
    /// <param name="ct">Cancellation token.</param>
    public static async ValueTask<ArchiveAppender> OpenAsync(
        string path,
        DariPassphrase? passphrase = null,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!File.Exists(path))
            throw new FileNotFoundException($"Archive not found: {path}", path);

        // Read existing archive index.
        using var existing = await DariReader.OpenAsync(path, ct).ConfigureAwait(false);
        var existingEntries = existing.Entries.ToList();
        long dataEnd = existing.IndexOffset;

        // Seed dedup tracker from existing primary entries.
        var dedup = new DeduplicationTracker(existingEntries.Count + 64);
        dedup.Seed(existingEntries);

        // Copy header + data blocks to temp file.
        string tempPath = path + ".tmp~";
        var tempFs = new FileStream(tempPath, FileMode.Create, FileAccess.ReadWrite,
                                    FileShare.None, bufferSize: 65536, useAsync: true);
        using (var src = new FileStream(path, FileMode.Open, FileAccess.Read,
                                        FileShare.Read, bufferSize: 65536, useAsync: true))
        {
            await CopyBytesAsync(src, tempFs, dataEnd, ct).ConfigureAwait(false);
        }

        // Resume a writer at dataEnd — no header written, existing data preserved.
        var writer = DariWriter.Resume(tempFs, leaveOpen: false, passphrase, dedup);

        return new ArchiveAppender(path, tempPath, writer, existingEntries);
    }

    // -----------------------------------------------------------------------
    // Adding new files
    // -----------------------------------------------------------------------

    /// <summary>Adds a file from disk.</summary>
    /// <param name="sourcePath">Path to the source file.</param>
    /// <param name="archivePath">Forward-slash path inside the archive.</param>
    /// <param name="ct">Cancellation token.</param>
    public async ValueTask AddAsync(string sourcePath, string archivePath,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(archivePath);
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_finalized) throw new InvalidOperationException("Appender has already been finalized.");

        var fi = new FileInfo(sourcePath);
        if (!fi.Exists)
            throw new FileNotFoundException($"Source file not found: {sourcePath}", sourcePath);

        var metadata = FileMetadata.FromFileInfo(fi);
        await using var fs = fi.Open(FileMode.Open, FileAccess.Read, FileShare.Read);
        await _writer.AddFileAsync(archivePath, fs, metadata, ct: ct).ConfigureAwait(false);
    }

    /// <summary>Adds a file from an in-memory buffer.</summary>
    /// <param name="archivePath">Forward-slash path inside the archive.</param>
    /// <param name="content">File content bytes.</param>
    /// <param name="metadata">File metadata (mtime, uid, gid, perm).</param>
    /// <param name="ct">Cancellation token.</param>
    public ValueTask AddAsync(string archivePath, ReadOnlyMemory<byte> content,
        FileMetadata metadata, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(archivePath);
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_finalized) throw new InvalidOperationException("Appender has already been finalized.");

        return _writer.AddFileAsync(archivePath, content, metadata, ct: ct);
    }

    // -----------------------------------------------------------------------
    // Finalize / commit
    // -----------------------------------------------------------------------

    /// <summary>
    /// Writes the combined index (existing + new) and footer, then atomically
    /// renames the temp file over the original.
    /// </summary>
    public async ValueTask FinalizeAsync(CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_finalized) return;
        _finalized = true;

        // Prepend existing entries so they appear first in the index.
        _writer.InjectEntries(_existingEntries);
        await _writer.FinalizeAsync(ct).ConfigureAwait(false);

        File.Move(_tempPath, _originalPath, overwrite: true);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;

        // Finalize before marking disposed — FinalizeAsync checks _disposed.
        if (!_finalized)
        {
            try { await FinalizeAsync().ConfigureAwait(false); }
            catch { /* best-effort on auto-finalize */ }
        }

        _disposed = true;

        await _writer.DisposeAsync().ConfigureAwait(false);

        if (File.Exists(_tempPath))
            try { File.Delete(_tempPath); } catch { /* ignore */ }
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static async Task CopyBytesAsync(Stream src, Stream dst, long count, CancellationToken ct)
    {
        byte[] buf = new byte[81920];
        long remaining = count;
        while (remaining > 0)
        {
            int toRead = (int)Math.Min(buf.Length, remaining);
            int read = await src.ReadAsync(buf.AsMemory(0, toRead), ct).ConfigureAwait(false);
            if (read == 0) break;
            await dst.WriteAsync(buf.AsMemory(0, read), ct).ConfigureAwait(false);
            remaining -= read;
        }
    }
}
