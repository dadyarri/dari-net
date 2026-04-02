using Dari.Archiver.Diagnostics;
using Dari.Archiver.Format;

namespace Dari.Archiver.Tests;

public sealed class DariHeaderTests
{
    [Fact]
    public void RoundTrip_PreservesTimestamp()
    {
        var ts = new DateTimeOffset(2025, 6, 15, 12, 0, 0, TimeSpan.Zero);
        var hdr = DariHeader.CreateWith(ts);

        Span<byte> buf = stackalloc byte[DariConstants.HeaderSize];
        hdr.WriteTo(buf);

        var read = DariHeader.ReadFrom(buf);
        Assert.Equal(ts, read.CreatedAt);
    }

    [Fact]
    public void ReadFrom_WrongMagic_Throws()
    {
        var buf = new byte[DariConstants.HeaderSize];
        "XARI"u8.CopyTo(buf);
        buf[4] = DariConstants.FormatVersion;

        Assert.Throws<DariFormatException>(() => DariHeader.ReadFrom(buf));
    }

    [Fact]
    public void ReadFrom_WrongVersion_Throws()
    {
        var buf = new byte[DariConstants.HeaderSize];
        DariHeader.CreateNew().WriteTo(buf);
        buf[4] = 3; // bad version

        var ex = Assert.Throws<DariFormatException>(() => DariHeader.ReadFrom(buf));
        Assert.Contains("version 3", ex.Message);
    }

    [Fact]
    public void WriteTo_ProducesDariMagic()
    {
        var buf = new byte[DariConstants.HeaderSize];
        DariHeader.CreateNew().WriteTo(buf);

        Assert.True(buf.AsSpan(0, 4).SequenceEqual("DARI"u8));
        Assert.Equal(DariConstants.FormatVersion, buf[4]);
    }
}
