using Dari.Archiver.Format;

namespace Dari.App.Models;

/// <summary>
/// UI-layer wrapper around an <see cref="IndexEntry"/> with display-ready computed properties.
/// </summary>
public sealed class ArchiveEntryViewModel
{
    private readonly IndexEntry _entry;

    public ArchiveEntryViewModel(IndexEntry entry) => _entry = entry;

    public IndexEntry Entry => _entry;

    /// <summary>File name without directory component.</summary>
    public string Name => System.IO.Path.GetFileName(_entry.Path);

    /// <summary>Archive-internal relative path.</summary>
    public string Path => _entry.Path;

    public ulong OriginalSize => _entry.OriginalSize;
    public ulong CompressedSize => _entry.CompressedSize;

    /// <summary>Compressed-to-original ratio, or <see langword="null"/> for empty files.</summary>
    public double? CompressionRatio => _entry.OriginalSize > 0
        ? (double)_entry.CompressedSize / _entry.OriginalSize
        : null;

    /// <summary>Human-readable ratio string, e.g. "42.3%".</summary>
    public string CompressionRatioDisplay => CompressionRatio is { } r ? $"{r:P1}" : "—";

    public bool IsEncrypted => _entry.IsEncrypted;
    public bool IsLinked => _entry.IsLinked;

    /// <summary>Display name of the compression algorithm.</summary>
    public string Algorithm => _entry.Compression.ToString();

    public DateTimeOffset ModifiedAt => _entry.ModifiedAt;

    /// <summary>Formatted original size (e.g. "1.2 MB").</summary>
    public string OriginalSizeDisplay => FormatSize(_entry.OriginalSize);

    /// <summary>Formatted compressed size (e.g. "500 KB").</summary>
    public string CompressedSizeDisplay => FormatSize(_entry.CompressedSize);

    /// <summary>Modification time formatted for the UI.</summary>
    public string ModifiedAtDisplay => _entry.ModifiedAt.LocalDateTime.ToString("yyyy-MM-dd HH:mm");

    /// <summary>Unix permission bits rendered as "rwxrwxrwx".</summary>
    public string PermissionsDisplay => FormatPermissions(_entry.Perm);

    /// <summary>Lowercase file extension including the dot (e.g. ".rs").</summary>
    public string Extension => System.IO.Path.GetExtension(_entry.Path).ToLowerInvariant();

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static string FormatSize(ulong bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
        < 1024UL * 1024 * 1024 => $"{bytes / (1024.0 * 1024):F1} MB",
        _ => $"{bytes / (1024.0 * 1024 * 1024):F2} GB",
    };

    private static string FormatPermissions(ushort perm)
    {
        Span<char> chars = stackalloc char[9];
        chars[0] = (perm & 0x100) != 0 ? 'r' : '-';
        chars[1] = (perm & 0x080) != 0 ? 'w' : '-';
        chars[2] = (perm & 0x040) != 0 ? 'x' : '-';
        chars[3] = (perm & 0x020) != 0 ? 'r' : '-';
        chars[4] = (perm & 0x010) != 0 ? 'w' : '-';
        chars[5] = (perm & 0x008) != 0 ? 'x' : '-';
        chars[6] = (perm & 0x004) != 0 ? 'r' : '-';
        chars[7] = (perm & 0x002) != 0 ? 'w' : '-';
        chars[8] = (perm & 0x001) != 0 ? 'x' : '-';
        return new string(chars);
    }
}
