using System.Text;
using Dari.Archiver.Diagnostics;
using Dari.Archiver.Format;
using Dari.Archiver.IO;

namespace Dari.Archiver.Tests;

public sealed class DariReaderTests
{
    private static readonly DateTimeOffset FixedTime =
        new(2025, 6, 15, 12, 0, 0, TimeSpan.Zero);

    // -----------------------------------------------------------------------
    // Happy-path: single entry
    // -----------------------------------------------------------------------

    [Fact]
    public async Task OpenAsync_SingleEntry_ReadsHeaderAndEntry()
    {
        var data = "hello world"u8.ToArray();
        using var ms = new ArchiveBuilder(FixedTime)
            .AddFile("src/hello.txt", data)
            .Build();

        await using var reader = await DariReader.OpenAsync(ms, leaveOpen: true);

        Assert.Equal(FixedTime, reader.Header.CreatedAt);
        Assert.Single(reader.Entries);

        var entry = reader.Entries[0];
        Assert.Equal("src/hello.txt", entry.Path);
        Assert.Equal((ulong)data.Length, entry.OriginalSize);
        Assert.Equal((ulong)data.Length, entry.CompressedSize);
        Assert.Equal(CompressionMethod.None, entry.Compression);
        Assert.False(entry.IsLinked);
        Assert.False(entry.IsEncrypted);
    }

    [Fact]
    public async Task OpenAsync_EntryOffset_PointsAfterHeader()
    {
        var data = "abc"u8.ToArray();
        using var ms = new ArchiveBuilder(FixedTime)
            .AddFile("a.txt", data)
            .Build();

        await using var reader = await DariReader.OpenAsync(ms, leaveOpen: true);

        // First data block must start right after the 13-byte header
        Assert.Equal((ulong)DariConstants.HeaderSize, reader.Entries[0].Offset);
    }

    // -----------------------------------------------------------------------
    // Happy-path: multiple entries
    // -----------------------------------------------------------------------

    [Fact]
    public async Task OpenAsync_MultipleEntries_AllParsed()
    {
        using var ms = new ArchiveBuilder(FixedTime)
            .AddFile("a.rs", "fn main() {}"u8.ToArray())
            .AddFile("b.json", "{}"u8.ToArray())
            .AddFile("c.txt", "hi"u8.ToArray())
            .Build();

        await using var reader = await DariReader.OpenAsync(ms, leaveOpen: true);

        Assert.Equal(3, reader.Entries.Count);
        Assert.Equal("a.rs", reader.Entries[0].Path);
        Assert.Equal("b.json", reader.Entries[1].Path);
        Assert.Equal("c.txt", reader.Entries[2].Path);
    }

    // -----------------------------------------------------------------------
    // Happy-path: zero entries
    // -----------------------------------------------------------------------

    [Fact]
    public async Task OpenAsync_EmptyArchive_EntriesEmpty()
    {
        using var ms = new ArchiveBuilder(FixedTime).Build();

        await using var reader = await DariReader.OpenAsync(ms, leaveOpen: true);

        Assert.Empty(reader.Entries);
    }

    // -----------------------------------------------------------------------
    // Happy-path: entry with extra fields
    // -----------------------------------------------------------------------

    [Fact]
    public async Task OpenAsync_EntryWithExtra_ExtraParsed()
    {
        using var ms = new ArchiveBuilder(FixedTime)
            .AddFile("enc.bin", new byte[] { 1, 2, 3 }, "e=chacha20poly1305;en=aabbccdd")
            .Build();

        await using var reader = await DariReader.OpenAsync(ms, leaveOpen: true);

        var entry = reader.Entries[0];
        Assert.Equal("chacha20poly1305", entry.Extra.GetValueOrDefault("e"));
        Assert.Equal("aabbccdd", entry.Extra.GetValueOrDefault("en"));
    }

    // -----------------------------------------------------------------------
    // ReadRawBlockAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ReadRawBlockAsync_ReturnsCorrectBytes()
    {
        var data = Encoding.UTF8.GetBytes("raw content here");
        using var ms = new ArchiveBuilder(FixedTime)
            .AddFile("file.txt", data)
            .Build();

        await using var reader = await DariReader.OpenAsync(ms, leaveOpen: true);
        var raw = await reader.ReadRawBlockAsync(reader.Entries[0]);

        Assert.Equal(data, raw.ToArray());
    }

    [Fact]
    public async Task ReadRawBlockAsync_MultipleFiles_ReturnsCorrectBytesEach()
    {
        var dataA = "AAAA"u8.ToArray();
        var dataB = "BBBBBBBB"u8.ToArray();
        using var ms = new ArchiveBuilder(FixedTime)
            .AddFile("a.txt", dataA)
            .AddFile("b.txt", dataB)
            .Build();

        await using var reader = await DariReader.OpenAsync(ms, leaveOpen: true);

        var rawA = await reader.ReadRawBlockAsync(reader.Entries[0]);
        var rawB = await reader.ReadRawBlockAsync(reader.Entries[1]);

        Assert.Equal(dataA, rawA.ToArray());
        Assert.Equal(dataB, rawB.ToArray());
    }

    // -----------------------------------------------------------------------
    // Validation failures
    // -----------------------------------------------------------------------

    [Fact]
    public async Task OpenAsync_TooShort_Throws()
    {
        var ms = new MemoryStream(new byte[10]);
        await Assert.ThrowsAsync<DariFormatException>(
            () => DariReader.OpenAsync(ms).AsTask());
    }

    [Fact]
    public async Task OpenAsync_BadHeaderMagic_Throws()
    {
        using var ms = new ArchiveBuilder(FixedTime).AddFile("x", new byte[1]).Build();
        // Corrupt the header magic
        ms.Position = 0;
        ms.WriteByte((byte)'X');
        ms.Position = 0;

        await Assert.ThrowsAsync<DariFormatException>(
            () => DariReader.OpenAsync(ms, leaveOpen: true).AsTask());
    }

    [Fact]
    public async Task OpenAsync_BadFooterMagic_Throws()
    {
        using var ms = new ArchiveBuilder(FixedTime).AddFile("x", new byte[1]).Build();
        // Corrupt the footer magic (last 15 bytes, first byte)
        ms.Position = ms.Length - DariConstants.FooterSize;
        ms.WriteByte((byte)'X');
        ms.Position = 0;

        await Assert.ThrowsAsync<DariFormatException>(
            () => DariReader.OpenAsync(ms, leaveOpen: true).AsTask());
    }

    // -----------------------------------------------------------------------
    // Non-seekable / unreadable stream guard
    // -----------------------------------------------------------------------

    [Fact]
    public void OpenAsync_NonSeekableStream_Throws()
    {
        var stream = new NonSeekableStream();
        Assert.Throws<ArgumentException>(() => DariReader.OpenAsync(stream));
    }

    // -----------------------------------------------------------------------
    // Dispose guard
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ReadRawBlockAsync_AfterDispose_Throws()
    {
        using var ms = new ArchiveBuilder(FixedTime).AddFile("x", new byte[] { 42 }).Build();
        var reader = await DariReader.OpenAsync(ms, leaveOpen: true);
        var entry = reader.Entries[0];

        await reader.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => reader.ReadRawBlockAsync(entry).AsTask());
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private sealed class NonSeekableStream : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => 0;
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
