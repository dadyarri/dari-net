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

    // -----------------------------------------------------------------------
    // Computation via Blake3.NET
    // -----------------------------------------------------------------------

    [Fact]
    public void Of_EmptyInput_ProducesKnownHash()
    {
        // BLAKE3 of empty bytes is a well-known constant.
        const string expected = "af1349b9f5f9a1a6a0404dea36dcc9499bcb25c9adc112b7cc9a93cae41f3262";
        var hash = Blake3Hash.Of(ReadOnlySpan<byte>.Empty);
        Assert.Equal(expected, hash.ToString());
    }

    [Fact]
    public void Of_SameInput_ProducesSameHash()
    {
        var data = "hello"u8.ToArray();
        Assert.Equal(Blake3Hash.Of(data), Blake3Hash.Of(data));
    }

    [Fact]
    public void Of_DifferentInput_ProducesDifferentHash()
    {
        Assert.NotEqual(Blake3Hash.Of("a"u8), Blake3Hash.Of("b"u8));
    }

    [Fact]
    public void DeriveKey_SameContextAndMaterial_ProducesSameKey()
    {
        var material = "passphrase"u8.ToArray();
        var k1 = Blake3Hash.DeriveKey("dari.v1.chacha20poly1305.key", material);
        var k2 = Blake3Hash.DeriveKey("dari.v1.chacha20poly1305.key", material);
        Assert.Equal(k1, k2);
    }

    [Fact]
    public void DeriveKey_DifferentContext_ProducesDifferentKey()
    {
        var material = "passphrase"u8.ToArray();
        var k1 = Blake3Hash.DeriveKey("context.a", material);
        var k2 = Blake3Hash.DeriveKey("context.b", material);
        Assert.NotEqual(k1, k2);
    }

    [Fact]
    public void DeriveKey_DifferentMaterial_ProducesDifferentKey()
    {
        const string ctx = "dari.v1.chacha20poly1305.key";
        var k1 = Blake3Hash.DeriveKey(ctx, "pass1"u8);
        var k2 = Blake3Hash.DeriveKey(ctx, "pass2"u8);
        Assert.NotEqual(k1, k2);
    }
}
