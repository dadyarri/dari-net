using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Text;
using Dari.Archiver.Extra;

namespace Dari.Archiver.Format;

/// <summary>
/// The fixed 85-byte portion of a Dari index entry (§6.1).
/// Must be read via <see cref="MemoryMarshal.Read{T}"/> after validating the buffer length.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal readonly struct IndexEntryFixed
{
    /// <summary>Absolute byte offset of this entry's data block in the archive.</summary>
    public readonly ulong Offset;

    /// <summary>Bit-field; see <see cref="IndexFlags"/>.</summary>
    public readonly IndexFlags BitFlags;

    /// <summary>Compression algorithm used for the data block.</summary>
    public readonly CompressionMethod Compression;

    /// <summary>File last-modified time (Unix epoch, seconds).</summary>
    public readonly ulong ModificationTimestamp;

    public readonly uint Uid;
    public readonly uint Gid;
    public readonly ushort Perm;

    /// <summary>BLAKE3 hash of the original (uncompressed, unencrypted) content.</summary>
    public readonly Blake3Hash Checksum;

    /// <summary>File size before compression and encryption.</summary>
    public readonly ulong OriginalSize;

    /// <summary>Byte length of the stored data block (after compression and/or encryption).</summary>
    public readonly ulong CompressedSize;

    /// <summary>Byte length of the UTF-8 path string that immediately follows this struct.</summary>
    public readonly uint PathLength;

    /// <summary>Byte length of the UTF-8 extra string that follows the path.</summary>
    public readonly uint ExtraLength;
}

/// <summary>
/// A fully parsed Dari index entry: fixed struct + variable-length path and extra fields (§6).
/// </summary>
public sealed class IndexEntry
{
    internal IndexEntryFixed Fixed { get; }

    /// <summary>Archive-internal relative path, forward-slash-separated (e.g. <c>src/main.rs</c>).</summary>
    public string Path { get; }

    /// <summary>Parsed extra key/value metadata.</summary>
    public ExtraField Extra { get; }

    // Convenience accessors forwarded from the fixed struct
    public ulong Offset => Fixed.Offset;
    public IndexFlags BitFlags => Fixed.BitFlags;
    public CompressionMethod Compression => Fixed.Compression;
    public DateTimeOffset ModifiedAt => DateTimeOffset.FromUnixTimeSeconds((long)Fixed.ModificationTimestamp);
    public uint Uid => Fixed.Uid;
    public uint Gid => Fixed.Gid;
    public ushort Perm => Fixed.Perm;
    public Blake3Hash Checksum => Fixed.Checksum;
    public ulong OriginalSize => Fixed.OriginalSize;
    public ulong CompressedSize => Fixed.CompressedSize;

    public bool IsLinked => (Fixed.BitFlags & IndexFlags.LinkedData) != 0;
    public bool IsEncrypted => (Fixed.BitFlags & IndexFlags.EncryptedData) != 0;

    internal IndexEntry(IndexEntryFixed fixedPart, string path, ExtraField extra)
    {
        Fixed = fixedPart;
        Path = path;
        Extra = extra;
    }

    /// <summary>
    /// Reads one <see cref="IndexEntry"/> from <paramref name="span"/> (starting at offset 0)
    /// and returns the number of bytes consumed.
    /// </summary>
    public static IndexEntry ReadFrom(ReadOnlySpan<byte> span, out int bytesConsumed)
    {
        if (span.Length < DariConstants.IndexEntryFixedSize)
            throw new ArgumentException(
                $"Buffer too small: need {DariConstants.IndexEntryFixedSize} bytes for the fixed struct, got {span.Length}.",
                nameof(span));

        var fixedPart = MemoryMarshal.Read<IndexEntryFixed>(span);

        int pathStart = DariConstants.IndexEntryFixedSize;
        int extraStart = pathStart + (int)fixedPart.PathLength;
        int end = extraStart + (int)fixedPart.ExtraLength;

        if (span.Length < end)
            throw new ArgumentException(
                $"Buffer too small: need {end} bytes for this entry, got {span.Length}.",
                nameof(span));

        string path = Encoding.UTF8.GetString(span.Slice(pathStart, (int)fixedPart.PathLength));
        ExtraField extra = fixedPart.ExtraLength > 0
            ? ExtraField.Parse(span.Slice(extraStart, (int)fixedPart.ExtraLength))
            : ExtraField.Empty;

        bytesConsumed = end;
        return new IndexEntry(fixedPart, path, extra);
    }

    /// <summary>
    /// Serialises this entry into <paramref name="destination"/>.
    /// Returns the total bytes written (85 + path bytes + extra bytes).
    /// </summary>
    public int WriteTo(Span<byte> destination)
    {
        byte[] pathBytes = Encoding.UTF8.GetBytes(Path);
        byte[] extraBytes = Encoding.UTF8.GetBytes(Extra.Serialize());

        int total = DariConstants.IndexEntryFixedSize + pathBytes.Length + extraBytes.Length;
        if (destination.Length < total)
            throw new ArgumentException($"Destination too small: need {total} bytes.", nameof(destination));

        // Write fixed struct via MemoryMarshal (must be a local for `in` parameter)
        var fixedLocal = Fixed;
        MemoryMarshal.Write(destination, in fixedLocal);

        // Overwrite PathLength and ExtraLength in case they differ from field values
        // (they should match, but let's be safe when building new entries)
        BinaryPrimitives.WriteUInt32LittleEndian(destination[77..], (uint)pathBytes.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(destination[81..], (uint)extraBytes.Length);

        pathBytes.CopyTo(destination[DariConstants.IndexEntryFixedSize..]);
        extraBytes.CopyTo(destination[(DariConstants.IndexEntryFixedSize + pathBytes.Length)..]);

        return total;
    }
}
