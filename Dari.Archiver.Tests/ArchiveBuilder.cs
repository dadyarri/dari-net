using System.Buffers.Binary;
using System.Text;
using Dari.Archiver.Format;

namespace Dari.Archiver.Tests;

/// <summary>
/// Builds minimal valid in-memory <c>.dar</c> archives for use in reader tests.
/// </summary>
internal sealed class ArchiveBuilder
{
    private record FileEntry(string Path, byte[] Data, string Extra = "");

    private readonly DateTimeOffset _createdAt;
    private readonly List<FileEntry> _files = new();

    public ArchiveBuilder(DateTimeOffset? createdAt = null)
        => _createdAt = createdAt ?? new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public ArchiveBuilder AddFile(string path, byte[] data, string extra = "")
    {
        _files.Add(new FileEntry(path, data, extra));
        return this;
    }

    public MemoryStream Build()
    {
        var ms = new MemoryStream();

        // ── Header ──────────────────────────────────────────────────────────
        var header = DariHeader.CreateWith(_createdAt);
        Span<byte> hBuf = stackalloc byte[DariConstants.HeaderSize];
        header.WriteTo(hBuf);
        ms.Write(hBuf);

        // ── Data blocks + index entries ──────────────────────────────────────
        var entries = new List<(byte[] pathBytes, byte[] extraBytes, ulong offset, ulong size, Blake3Hash checksum)>();

        foreach (var file in _files)
        {
            ulong offset = (ulong)ms.Position;
            ms.Write(file.Data);

            byte[] pathBytes = Encoding.UTF8.GetBytes(file.Path);
            byte[] extraBytes = Encoding.UTF8.GetBytes(file.Extra);
            var checksum = MakeChecksum(file.Data);
            entries.Add((pathBytes, extraBytes, offset, (ulong)file.Data.Length, checksum));
        }

        // ── Index ────────────────────────────────────────────────────────────
        uint indexOffset = (uint)ms.Position;

        foreach (var (pathBytes, extraBytes, offset, size, checksum) in entries)
        {
            WriteIndexEntry(ms, offset, size, pathBytes, extraBytes, checksum);
        }

        // ── Footer ───────────────────────────────────────────────────────────
        var footer = DariFooter.Create(indexOffset, (uint)_files.Count);
        Span<byte> fBuf = stackalloc byte[DariConstants.FooterSize];
        footer.WriteTo(fBuf);
        ms.Write(fBuf);

        ms.Position = 0;
        return ms;
    }

    // Build a fake but consistent 32-byte checksum from the data bytes.
    private static Blake3Hash MakeChecksum(byte[] data)
    {
        var buf = new byte[32];
        for (int i = 0; i < Math.Min(data.Length, 32); i++)
            buf[i] = data[i];
        buf[0] ^= 0xFF; // make it distinguishable from raw data
        return new Blake3Hash(buf);
    }

    private static void WriteIndexEntry(
        MemoryStream ms,
        ulong offset, ulong size,
        byte[] pathBytes, byte[] extraBytes,
        Blake3Hash checksum)
    {
        // Fixed 85-byte struct written field by field
        var buf = new byte[DariConstants.IndexEntryFixedSize];
        BinaryPrimitives.WriteUInt64LittleEndian(buf.AsSpan(0), offset);
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(8), 0);         // bitflags
        buf[10] = (byte)CompressionMethod.None;                               // compression
        BinaryPrimitives.WriteUInt64LittleEndian(buf.AsSpan(11), 1_700_000_000); // mtime
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(19), DariConstants.DefaultUid);
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(23), DariConstants.DefaultGid);
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(27), DariConstants.DefaultPerm);
        checksum.CopyTo(buf.AsSpan(29));                                      // checksum[32]
        BinaryPrimitives.WriteUInt64LittleEndian(buf.AsSpan(61), size);       // original_size
        BinaryPrimitives.WriteUInt64LittleEndian(buf.AsSpan(69), size);       // compressed_size
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(77), (uint)pathBytes.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(81), (uint)extraBytes.Length);

        ms.Write(buf);
        ms.Write(pathBytes);
        ms.Write(extraBytes);
    }
}
