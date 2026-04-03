using Dari.Archiver.Diagnostics;
using Dari.Archiver.Format;
using Dari.Archiver.IO;

namespace Dari.Archiver.Tests;

/// <summary>Tests for Phase 10: format validation / error handling.</summary>
public sealed class FormatValidationTests
{
    private static async Task<MemoryStream> MakeMinimalArchiveAsync()
    {
        var ms = new MemoryStream();
        await using var writer = await DariWriter.CreateAsync(ms, leaveOpen: true);
        await writer.AddFileAsync("x.txt", "x"u8.ToArray(),
            new FileMetadata(DateTimeOffset.UtcNow, 0, 0, 33188));
        await writer.FinalizeAsync();
        ms.Position = 0;
        return ms;
    }

    // -----------------------------------------------------------------------
    // Truncated / short file
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Open_EmptyStream_ThrowsDariFormatException()
    {
        var ms = new MemoryStream();
        await Assert.ThrowsAsync<DariFormatException>(() =>
            DariReader.OpenAsync(ms).AsTask());
    }

    [Fact]
    public async Task Open_TruncatedFile_ThrowsDariFormatException()
    {
        // Only first 4 bytes (not enough for full header).
        var ms = new MemoryStream(new byte[] { 0x44, 0x41, 0x52, 0x00 });
        await Assert.ThrowsAsync<DariFormatException>(() =>
            DariReader.OpenAsync(ms).AsTask());
    }

    // -----------------------------------------------------------------------
    // Bad magic
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Open_BadHeaderMagic_ThrowsDariFormatException()
    {
        var good = await MakeMinimalArchiveAsync();
        byte[] data = good.ToArray();
        // Corrupt the first byte of the header magic.
        data[0] = 0xFF;

        await Assert.ThrowsAsync<DariFormatException>(() =>
            DariReader.OpenAsync(new MemoryStream(data)).AsTask());
    }

    [Fact]
    public async Task Open_BadFooterMagic_ThrowsDariFormatException()
    {
        var good = await MakeMinimalArchiveAsync();
        byte[] data = good.ToArray();
        // Footer is the last 15 bytes; its magic is bytes 0–6 of the footer.
        // Corrupt the first byte of the footer magic.
        data[^15] = 0xFF;

        await Assert.ThrowsAsync<DariFormatException>(() =>
            DariReader.OpenAsync(new MemoryStream(data)).AsTask());
    }

    // -----------------------------------------------------------------------
    // Bad version
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Open_UnsupportedVersion_ThrowsDariFormatException()
    {
        var good = await MakeMinimalArchiveAsync();
        byte[] data = good.ToArray();
        // Version byte is at offset 4 (after the 4-byte magic).
        data[4] = 99; // not version 5

        await Assert.ThrowsAsync<DariFormatException>(() =>
            DariReader.OpenAsync(new MemoryStream(data)).AsTask());
    }

    // -----------------------------------------------------------------------
    // Corrupted index offset
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Open_IndexOffsetBeyondEof_ThrowsDariFormatException()
    {
        var good = await MakeMinimalArchiveAsync();
        byte[] data = good.ToArray();

        // Footer layout: [magic(7)][indexOffset(4)][fileCount(4)] — total 15 bytes.
        // indexOffset starts at data.Length - 8 (4 bytes for fileCount + 4 bytes for indexOffset).
        int footerIndexOffsetPos = data.Length - 8;
        // Write an offset larger than the file.
        uint badOffset = (uint)(data.Length + 999_999);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(
            data.AsSpan(footerIndexOffsetPos), badOffset);

        await Assert.ThrowsAsync<DariFormatException>(() =>
            DariReader.OpenAsync(new MemoryStream(data)).AsTask());
    }

    // -----------------------------------------------------------------------
    // Valid archive round-trip (sanity)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Open_ValidArchive_DoesNotThrow()
    {
        var ms = await MakeMinimalArchiveAsync();
        using var reader = await DariReader.OpenAsync(ms);
        Assert.NotNull(reader);
        Assert.Single(reader.Entries);
    }
}
