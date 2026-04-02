using System.Buffers.Binary;
using Dari.Archiver.Diagnostics;
using Dari.Archiver.Format;

namespace Dari.Archiver.Tests;

public sealed class DariFooterTests
{
    private static void WriteFooter(Span<byte> buf, uint indexOffset, uint fileCount)
    {
        DariConstants.FooterMagic.CopyTo(buf);
        BinaryPrimitives.WriteUInt32LittleEndian(buf[7..], indexOffset);
        BinaryPrimitives.WriteUInt32LittleEndian(buf[11..], fileCount);
    }

    [Fact]
    public void RoundTrip_PreservesValues()
    {
        const long fileLength = 1024;
        var footer = DariFooter.Create(100, 42);

        Span<byte> buf = stackalloc byte[DariConstants.FooterSize];
        footer.WriteTo(buf);

        var read = DariFooter.ReadFrom(buf, fileLength);
        Assert.Equal(100u, read.IndexOffset);
        Assert.Equal(42u, read.FileCount);
    }

    [Fact]
    public void ReadFrom_WrongMagic_Throws()
    {
        var buf = new byte[DariConstants.FooterSize];
        WriteFooter(buf, 13, 0);
        buf[0] = (byte)'X'; // corrupt magic

        Assert.Throws<DariFormatException>(() => DariFooter.ReadFrom(buf, 1024));
    }

    [Fact]
    public void ReadFrom_FileTooShort_Throws()
    {
        var buf = new byte[DariConstants.FooterSize];
        WriteFooter(buf, 13, 0);

        Assert.Throws<DariFormatException>(() => DariFooter.ReadFrom(buf, fileLength: 10));
    }

    [Fact]
    public void ReadFrom_IndexOffsetBelowHeader_Throws()
    {
        var buf = new byte[DariConstants.FooterSize];
        WriteFooter(buf, indexOffset: 5, fileCount: 0); // < 13 → invalid

        Assert.Throws<DariFormatException>(() => DariFooter.ReadFrom(buf, fileLength: 1024));
    }

    [Fact]
    public void ReadFrom_IndexOffsetPastFooter_Throws()
    {
        const long fileLength = 100;
        var buf = new byte[DariConstants.FooterSize];
        // offset = 90, footer at 85 → 90 > 100-15=85 → invalid (strictly greater)
        WriteFooter(buf, indexOffset: 90, fileCount: 0);

        Assert.Throws<DariFormatException>(() => DariFooter.ReadFrom(buf, fileLength));
    }

    [Fact]
    public void ReadFrom_IndexOffsetAtFooterStart_Valid()
    {
        // edge: index_offset == file_length - footer_size → valid (empty index at footer boundary)
        const long fileLength = 100;
        uint indexOffset = (uint)(fileLength - DariConstants.FooterSize); // 85
        var buf = new byte[DariConstants.FooterSize];
        WriteFooter(buf, indexOffset, fileCount: 0);

        var footer = DariFooter.ReadFrom(buf, fileLength);
        Assert.Equal(indexOffset, footer.IndexOffset);
    }
}
