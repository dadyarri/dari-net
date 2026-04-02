using System.Buffers.Binary;
using System.Text;
using Dari.Archiver.Extra;
using Dari.Archiver.Format;

namespace Dari.Archiver.Tests;

public sealed class IndexEntryTests
{
    /// <summary>Builds a minimal valid byte buffer for one IndexEntry.</summary>
    private static byte[] BuildEntryBytes(string path, string extra = "")
    {
        byte[] pathBytes = Encoding.UTF8.GetBytes(path);
        byte[] extraBytes = Encoding.UTF8.GetBytes(extra);
        int total = DariConstants.IndexEntryFixedSize + pathBytes.Length + extraBytes.Length;
        var buf = new byte[total];

        // offset (8)
        BinaryPrimitives.WriteUInt64LittleEndian(buf.AsSpan(0), 13);
        // bitflags (2) — None
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(8), 0);
        // compression_method (1) — Zstandard
        buf[10] = (byte)CompressionMethod.Zstandard;
        // modification_timestamp (8)
        BinaryPrimitives.WriteUInt64LittleEndian(buf.AsSpan(11), 1_700_000_000);
        // uid (4)
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(19), 1000);
        // gid (4)
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(23), 1000);
        // perm (2)
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(27), 644);
        // checksum (32) — all 0xAB
        buf.AsSpan(29, 32).Fill(0xAB);
        // original_size (8)
        BinaryPrimitives.WriteUInt64LittleEndian(buf.AsSpan(61), 12345);
        // compressed_size (8)
        BinaryPrimitives.WriteUInt64LittleEndian(buf.AsSpan(69), 6789);
        // path_length (4)
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(77), (uint)pathBytes.Length);
        // extra_length (4)
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(81), (uint)extraBytes.Length);

        pathBytes.CopyTo(buf, DariConstants.IndexEntryFixedSize);
        extraBytes.CopyTo(buf, DariConstants.IndexEntryFixedSize + pathBytes.Length);

        return buf;
    }

    [Fact]
    public void ReadFrom_ParsesPathAndFixedFields()
    {
        var buf = BuildEntryBytes("src/main.rs");
        var entry = IndexEntry.ReadFrom(buf, out int consumed);

        Assert.Equal("src/main.rs", entry.Path);
        Assert.Equal(13UL, entry.Offset);
        Assert.Equal(CompressionMethod.Zstandard, entry.Compression);
        Assert.Equal(12345UL, entry.OriginalSize);
        Assert.Equal(6789UL, entry.CompressedSize);
        Assert.Equal(1000u, entry.Uid);
        Assert.Equal(1000u, entry.Gid);
        Assert.Equal((ushort)644, entry.Perm);
        Assert.False(entry.IsLinked);
        Assert.False(entry.IsEncrypted);
        Assert.Equal(DariConstants.IndexEntryFixedSize + "src/main.rs"u8.Length, consumed);
    }

    [Fact]
    public void ReadFrom_ParsesChecksum()
    {
        var buf = BuildEntryBytes("file.txt");
        var entry = IndexEntry.ReadFrom(buf, out _);

        var expected = new byte[32];
        expected.AsSpan().Fill(0xAB);
        Span<byte> actual = stackalloc byte[32];
        entry.Checksum.CopyTo(actual);

        Assert.True(actual.SequenceEqual(expected));
    }

    [Fact]
    public void ReadFrom_ParsesExtraField()
    {
        var buf = BuildEntryBytes("x.cs", "e=chacha20poly1305");
        var entry = IndexEntry.ReadFrom(buf, out _);

        Assert.Equal("chacha20poly1305", entry.Extra.GetValueOrDefault("e"));
    }

    [Fact]
    public void ReadFrom_EmptyExtra_ExtraIsEmpty()
    {
        var buf = BuildEntryBytes("a.txt", "");
        var entry = IndexEntry.ReadFrom(buf, out _);

        Assert.Equal(0, entry.Extra.Count);
    }

    [Fact]
    public void ReadFrom_LinkedFlag_IsLinkedTrue()
    {
        var buf = BuildEntryBytes("dup.bin");
        // Set LinkedData flag
        BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(8), (ushort)IndexFlags.LinkedData);

        var entry = IndexEntry.ReadFrom(buf, out _);
        Assert.True(entry.IsLinked);
        Assert.False(entry.IsEncrypted);
    }

    [Fact]
    public void ReadFrom_BufferTooSmall_Throws()
    {
        var buf = new byte[10]; // far too small
        Assert.Throws<ArgumentException>(() => IndexEntry.ReadFrom(buf, out _));
    }

    [Fact]
    public void ModifiedAt_CorrectlyConvertsTimestamp()
    {
        var buf = BuildEntryBytes("ts.txt");
        var entry = IndexEntry.ReadFrom(buf, out _);

        // 1_700_000_000 Unix seconds → 2023-11-14T22:13:20Z
        Assert.Equal(2023, entry.ModifiedAt.Year);
        Assert.Equal(11, entry.ModifiedAt.Month);
    }
}
