using Dari.Archiver.Compression;
using Dari.Archiver.Crypto;
using Dari.Archiver.Format;
using Dari.Archiver.Ignoring;
using Dari.Archiver.IO;

namespace Dari.Archiver.Archiving;

/// <summary>
/// High-level archive writer with a fluent, file-system-oriented API.
/// Wraps <see cref="DariWriter"/> and auto-finalizes on <see cref="DisposeAsync"/>.
/// </summary>
/// <remarks>
/// <para>
/// Typical usage:
/// <code>
/// await using var writer = await ArchiveWriter.CreateAsync("out.dar");
/// await writer.AddAsync("/path/to/file.txt", "docs/file.txt");
/// await writer.AddDirectoryAsync("/path/to/dir", "src/");
/// // Finalizes automatically on dispose.
/// </code>
/// </para>
/// <para>
/// Call <see cref="FinalizeAsync"/> explicitly before dispose if you need to catch errors from
/// index/footer writing.
/// </para>
/// </remarks>
public sealed class ArchiveWriter : IAsyncDisposable
{
    private readonly DariWriter _inner;
    private readonly CompressorRegistry _registry;
    private bool _finalized;

    private ArchiveWriter(DariWriter inner, CompressorRegistry registry)
    {
        _inner = inner;
        _registry = registry;
    }

    // -----------------------------------------------------------------------
    // Factory methods
    // -----------------------------------------------------------------------

    /// <summary>
    /// Creates a new archive at <paramref name="path"/>.
    /// </summary>
    /// <param name="path">Output file path. The file is created or truncated.</param>
    /// <param name="compressors">Compressor registry; defaults to <see cref="CompressorRegistry.Default"/>.</param>
    /// <param name="passphrase">When non-null, all data blocks are encrypted with ChaCha20-Poly1305.</param>
    /// <param name="header">Archive header; defaults to a new header stamped with the current time.</param>
    /// <param name="ct">Cancellation token.</param>
    public static async ValueTask<ArchiveWriter> CreateAsync(
        string path,
        CompressorRegistry? compressors = null,
        DariPassphrase? passphrase = null,
        DariHeader? header = null,
        CancellationToken ct = default)
    {
        var inner = await DariWriter.CreateAsync(path, header, passphrase, ct).ConfigureAwait(false);
        return new ArchiveWriter(inner, compressors ?? CompressorRegistry.Default);
    }

    /// <summary>
    /// Creates a new archive that writes to <paramref name="stream"/>.
    /// </summary>
    /// <param name="stream">A writable, seekable stream.</param>
    /// <param name="leaveOpen">When <see langword="true"/> the stream is not disposed with the writer.</param>
    /// <param name="compressors">Compressor registry; defaults to <see cref="CompressorRegistry.Default"/>.</param>
    /// <param name="passphrase">When non-null, all data blocks are encrypted with ChaCha20-Poly1305.</param>
    /// <param name="header">Archive header; defaults to a new header stamped with the current time.</param>
    /// <param name="ct">Cancellation token.</param>
    public static async ValueTask<ArchiveWriter> CreateAsync(
        Stream stream,
        bool leaveOpen = false,
        CompressorRegistry? compressors = null,
        DariPassphrase? passphrase = null,
        DariHeader? header = null,
        CancellationToken ct = default)
    {
        var inner = await DariWriter.CreateAsync(stream, header, leaveOpen, passphrase, ct).ConfigureAwait(false);
        return new ArchiveWriter(inner, compressors ?? CompressorRegistry.Default);
    }

    // -----------------------------------------------------------------------
    // Adding content
    // -----------------------------------------------------------------------

    /// <summary>
    /// Reads the file at <paramref name="sourcePath"/>, selects a compressor based on its
    /// extension, and adds it to the archive at <paramref name="archivePath"/>.
    /// </summary>
    /// <param name="sourcePath">Absolute or relative path to the source file on disk.</param>
    /// <param name="archivePath">
    ///   Forward-slash-separated path to store in the archive index
    ///   (e.g. <c>docs/readme.md</c>).
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    public async ValueTask AddAsync(
        string sourcePath,
        string archivePath,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(archivePath);

        var fi = new FileInfo(sourcePath);
        if (!fi.Exists)
            throw new FileNotFoundException($"Source file not found: {sourcePath}", sourcePath);

        var metadata = FileMetadata.FromFileInfo(fi);
        await using var fs = fi.Open(FileMode.Open, FileAccess.Read, FileShare.Read);
        await _inner.AddFileAsync(archivePath, fs, metadata, _registry, ct: ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Adds a single file from a stream, using <paramref name="metadata"/> for index entry fields.
    /// </summary>
    /// <param name="content">Readable stream containing the file content.</param>
    /// <param name="archivePath">Forward-slash path to store in the archive index.</param>
    /// <param name="metadata">File metadata (mtime, uid, gid, perm).</param>
    /// <param name="ct">Cancellation token.</param>
    public ValueTask AddAsync(
        Stream content,
        string archivePath,
        FileMetadata metadata,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(archivePath);
        return _inner.AddFileAsync(archivePath, content, metadata, _registry, ct: ct);
    }

    /// <summary>
    /// Adds a single file from an in-memory buffer, using <paramref name="metadata"/> for index entry fields.
    /// </summary>
    /// <param name="archivePath">Forward-slash path to store in the archive index.</param>
    /// <param name="content">File content bytes.</param>
    /// <param name="metadata">File metadata (mtime, uid, gid, perm).</param>
    /// <param name="ct">Cancellation token.</param>
    public ValueTask AddAsync(
        string archivePath,
        ReadOnlyMemory<byte> content,
        FileMetadata metadata,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(archivePath);
        return _inner.AddFileAsync(archivePath, content, metadata, _registry, ct: ct);
    }

    /// <summary>
    /// Recursively adds all files under <paramref name="sourceDirectory"/> to the archive,
    /// respecting <c>.darignore</c> and <c>.gitignore</c> files found in the tree.
    /// </summary>
    /// <param name="sourceDirectory">Root directory to walk.</param>
    /// <param name="archivePrefix">
    ///   Prefix prepended to all archive paths (e.g. <c>src/</c>).
    ///   Use an empty string to store files without a prefix.
    /// </param>
    /// <param name="ignoreFilter">
    ///   Custom ignore filter. When <see langword="null"/>, a <see cref="GitIgnoreFilter"/>
    ///   is built automatically by scanning for <c>.darignore</c> / <c>.gitignore</c> files.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    public async ValueTask AddDirectoryAsync(
        string sourceDirectory,
        string archivePrefix = "",
        IIgnoreFilter? ignoreFilter = null,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceDirectory);

        var root = new DirectoryInfo(sourceDirectory);
        if (!root.Exists)
            throw new DirectoryNotFoundException($"Source directory not found: {sourceDirectory}");

        string prefix = archivePrefix.Length > 0 && !archivePrefix.EndsWith('/')
            ? archivePrefix + '/'
            : archivePrefix;

        IIgnoreFilter filter = ignoreFilter ?? GitIgnoreFilter.Load(sourceDirectory);

        await WalkDirectoryAsync(root, root, prefix, filter, ct).ConfigureAwait(false);
    }

    private async ValueTask WalkDirectoryAsync(
        DirectoryInfo root,
        DirectoryInfo current,
        string prefix,
        IIgnoreFilter filter,
        CancellationToken ct)
    {
        foreach (var fi in current.EnumerateFiles())
        {
            ct.ThrowIfCancellationRequested();

            string relPath = Path.GetRelativePath(root.FullName, fi.FullName)
                                 .Replace(Path.DirectorySeparatorChar, '/');
            if (filter.ShouldIgnore(relPath, isDirectory: false)) continue;

            string archivePath = prefix + relPath;
            var metadata = FileMetadata.FromFileInfo(fi);
            await using var fs = fi.Open(FileMode.Open, FileAccess.Read, FileShare.Read);
            await _inner.AddFileAsync(archivePath, fs, metadata, _registry, ct: ct)
                        .ConfigureAwait(false);
        }

        foreach (var sub in current.EnumerateDirectories())
        {
            ct.ThrowIfCancellationRequested();

            string relPath = Path.GetRelativePath(root.FullName, sub.FullName)
                                 .Replace(Path.DirectorySeparatorChar, '/');
            if (filter.ShouldIgnore(relPath, isDirectory: true)) continue;

            await WalkDirectoryAsync(root, sub, prefix, filter, ct).ConfigureAwait(false);
        }
    }

    // -----------------------------------------------------------------------
    // Finalize
    // -----------------------------------------------------------------------

    /// <summary>
    /// Writes the index and footer, then flushes the output stream.
    /// Called automatically by <see cref="DisposeAsync"/> if not called explicitly.
    /// </summary>
    public async ValueTask FinalizeAsync(CancellationToken ct = default)
    {
        if (_finalized) return;
        _finalized = true;
        await _inner.FinalizeAsync(ct).ConfigureAwait(false);
    }

    // -----------------------------------------------------------------------
    // Dispose
    // -----------------------------------------------------------------------

    /// <summary>
    /// Finalizes the archive (if not already done) and disposes the underlying stream.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        await FinalizeAsync().ConfigureAwait(false);
        await _inner.DisposeAsync().ConfigureAwait(false);
    }
}
