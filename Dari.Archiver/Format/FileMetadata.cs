namespace Dari.Archiver.Format;

/// <summary>
/// File metadata recorded in an index entry (§6.1).
/// On non-Unix platforms, <see cref="Uid"/>, <see cref="Gid"/>, and <see cref="Perm"/>
/// default to the Dari placeholders (1000 / 1000 / 644).
/// </summary>
public readonly struct FileMetadata
{
    public DateTimeOffset ModifiedAt { get; }
    public uint Uid { get; }
    public uint Gid { get; }
    public ushort Perm { get; }

    public FileMetadata(DateTimeOffset modifiedAt, uint uid = DariConstants.DefaultUid,
                        uint gid = DariConstants.DefaultGid, ushort perm = DariConstants.DefaultPerm)
    {
        ModifiedAt = modifiedAt;
        Uid = uid;
        Gid = gid;
        Perm = perm;
    }

    /// <summary>
    /// Creates <see cref="FileMetadata"/> from a <see cref="FileInfo"/>, using Unix metadata
    /// when running on a Unix-like OS and Dari placeholder values on Windows.
    /// </summary>
    public static FileMetadata FromFileInfo(FileInfo fi)
    {
        var mtime = new DateTimeOffset(fi.LastWriteTimeUtc);

        if (!OperatingSystem.IsWindows())
        {
            // Mono/CoreCLR on Linux/macOS exposes UnixFileMode
            var mode = fi.UnixFileMode;
            ushort perm = (ushort)((int)mode & 0xFFF);
            return new FileMetadata(mtime, DariConstants.DefaultUid, DariConstants.DefaultGid, perm);
        }

        return new FileMetadata(mtime);
    }

    /// <summary>Creates a <see cref="FileMetadata"/> stamped with the current UTC time and default Unix values.</summary>
    public static FileMetadata Now() => new(DateTimeOffset.UtcNow);
}
