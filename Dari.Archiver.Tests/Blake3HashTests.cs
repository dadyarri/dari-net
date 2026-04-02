using Dari.Archiver.Format;

namespace Dari.Archiver.Tests;

public sealed class Blake3HashTests
{
    private static byte[] MakeBytes(byte fill = 0xAB)
    {
        var b = new byte[32];
        b.AsSpan().Fill(fill);
        return b;
    }

    [Fact]
    public void RoundTrip_CopyTo()
    {
        var original = MakeBytes(0x77);
        var hash = new Blake3Hash(original);

        Span<byte> out1 = stackalloc byte[32];
        hash.CopyTo(out1);

        Assert.True(out1.SequenceEqual(original));
    }

    [Fact]
    public void Equality_SameBytes_Equal()
    {
        var a = new Blake3Hash(MakeBytes(0x01));
        var b = new Blake3Hash(MakeBytes(0x01));
        Assert.Equal(a, b);
        Assert.True(a == b);
    }

    [Fact]
    public void Equality_DifferentBytes_NotEqual()
    {
        var a = new Blake3Hash(MakeBytes(0x01));
        var b = new Blake3Hash(MakeBytes(0x02));
        Assert.NotEqual(a, b);
        Assert.True(a != b);
    }

    [Fact]
    public void GetHashCode_SameBytes_SameHashCode()
    {
        var a = new Blake3Hash(MakeBytes(0x55));
        var b = new Blake3Hash(MakeBytes(0x55));
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void CopyNonceTo_ProducesFirst12Bytes()
    {
        var bytes = new byte[32];
        for (int i = 0; i < 32; i++) bytes[i] = (byte)i;
        var hash = new Blake3Hash(bytes);

        Span<byte> nonce = stackalloc byte[12];
        hash.CopyNonceTo(nonce);

        // The nonce must match bytes[0..12] stored in LE order.
        // CopyTo(32) + comparing first 12 bytes is the simplest cross-check.
        Span<byte> full = stackalloc byte[32];
        hash.CopyTo(full);
        Assert.True(nonce.SequenceEqual(full[..12]));
    }

    [Fact]
    public void Constructor_TooShort_Throws()
    {
        Assert.Throws<ArgumentException>(() => new Blake3Hash(new byte[10]));
    }

    [Fact]
    public void ToString_Returns64HexChars()
    {
        var hash = new Blake3Hash(MakeBytes(0xFF));
        Assert.Equal(64, hash.ToString().Length);
        Assert.Matches("^[0-9a-f]{64}$", hash.ToString());
    }

    [Fact]
    public void UsableAsDictionaryKey()
    {
        var dict = new Dictionary<Blake3Hash, string>();
        var key = new Blake3Hash(MakeBytes(0x11));
        dict[key] = "value";
        Assert.Equal("value", dict[new Blake3Hash(MakeBytes(0x11))]);
    }
}
