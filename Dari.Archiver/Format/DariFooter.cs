using System.Buffers.Binary;
using Dari.Archiver.Diagnostics;

namespace Dari.Archiver.Format;

/// <summary>
/// Dari archive footer — always the last 15 bytes of the file (§5).
/// <code>
/// Bytes  0–6   signature    "DARIEND"
/// Bytes  7–10  index_offset  u32 LE  (absolute offset of first IndexEntry)
/// Bytes 11–14  file_count    u32 LE  (number of index entries)
/// </code>
/// </summary>
public readonly struct DariFooter
{
    /// <summary>Absolute byte offset of the first <see cref="IndexEntry"/> in the archive.</summary>
    public uint IndexOffset { get; }

    /// <summary>Total number of entries in the index.</summary>
    public uint FileCount { get; }

    private DariFooter(uint indexOffset, uint fileCount)
    {
        IndexOffset = indexOffset;
        FileCount = fileCount;
    }

    /// <summary>
    /// Reads and validates a <see cref="DariFooter"/> from exactly 15 bytes.
    /// <paramref name="fileLength"/> is required to validate <see cref="IndexOffset"/>.
    /// Throws <see cref="DariFormatException"/> on any violation.
    /// </summary>
    public static DariFooter ReadFrom(ReadOnlySpan<byte> span, long fileLength)
    {
        if (fileLength < DariConstants.MinArchiveSize)
            throw DariFormatException.FileTooShort(fileLength);

        if (!span[..7].SequenceEqual(DariConstants.FooterMagic))
            throw DariFormatException.BadFooterMagic();

        uint indexOffset = BinaryPrimitives.ReadUInt32LittleEndian(span[7..]);
        uint fileCount = BinaryPrimitives.ReadUInt32LittleEndian(span[11..]);

        // index_offset must be inside the file, after the header, and before (or at) the footer
        if (indexOffset < DariConstants.HeaderSize || indexOffset > fileLength - DariConstants.FooterSize)
            throw DariFormatException.BadIndexOffset(indexOffset, fileLength);

        return new DariFooter(indexOffset, fileCount);
    }

    /// <summary>Writes this footer into the first 15 bytes of <paramref name="span"/>.</summary>
    public void WriteTo(Span<byte> span)
    {
        DariConstants.FooterMagic.CopyTo(span);
        BinaryPrimitives.WriteUInt32LittleEndian(span[7..], IndexOffset);
        BinaryPrimitives.WriteUInt32LittleEndian(span[11..], FileCount);
    }

    public static DariFooter Create(uint indexOffset, uint fileCount) =>
        new(indexOffset, fileCount);
}
