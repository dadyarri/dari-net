using System.Buffers;
using Dari.Archiver.Compression;
using Dari.Archiver.Format;

namespace Dari.Archiver.Tests;

public class CompressionTests
{
    private static ArrayBufferWriter<byte> MakeWriter() => new ArrayBufferWriter<byte>();

    // 1. NoneCompressor round-trip
    [Fact]
    public async Task NoneCompressor_RoundTrip()
    {
        var c = new NoneCompressor();
        var data = "Hello, World!"u8.ToArray();
        var compressed = await c.CompressAsync(data);
        Assert.NotNull(compressed);

        var writer = MakeWriter();
        await c.DecompressAsync(compressed!.Value, (ulong)data.Length, writer);
        Assert.Equal(data, writer.WrittenSpan.ToArray());
    }

    // 2. BrotliCompressor round-trip with repetitive text
    [Fact]
    public async Task BrotliCompressor_RoundTrip_RepetitiveText()
    {
        var c = new BrotliCompressor();
        var data = System.Text.Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("Hello World! ", 200)));
        var compressed = await c.CompressAsync(data);
        Assert.NotNull(compressed);
        Assert.True(compressed!.Value.Length < data.Length, "Brotli should compress repetitive text");

        var writer = MakeWriter();
        await c.DecompressAsync(compressed.Value, (ulong)data.Length, writer);
        Assert.Equal(data, writer.WrittenSpan.ToArray());
    }

    // 3. ZstandardCompressor round-trip with repetitive text
    [Fact]
    public async Task ZstandardCompressor_RoundTrip_RepetitiveText()
    {
        var c = new ZstandardCompressor();
        var data = System.Text.Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("Hello World! ", 200)));
        var compressed = await c.CompressAsync(data);
        Assert.NotNull(compressed);
        Assert.True(compressed!.Value.Length < data.Length, "Zstandard should compress repetitive text");

        var writer = MakeWriter();
        await c.DecompressAsync(compressed.Value, (ulong)data.Length, writer);
        Assert.Equal(data, writer.WrittenSpan.ToArray());
    }

    // 4. LzmaCompressor round-trip with repetitive binary data
    [Fact]
    public async Task LzmaCompressor_RoundTrip_RepetitiveBinary()
    {
        var c = new LzmaCompressor();
        var data = new byte[2000];
        for (int i = 0; i < data.Length; i++) data[i] = (byte)(i % 50);
        var compressed = await c.CompressAsync(data);
        Assert.NotNull(compressed);
        Assert.True(compressed!.Value.Length < data.Length, "LZMA should compress repetitive binary data");

        var writer = MakeWriter();
        await c.DecompressAsync(compressed.Value, (ulong)data.Length, writer);
        Assert.Equal(data, writer.WrittenSpan.ToArray());
    }

    // 5a. BrotliCompressor returns null for incompressible 1-byte input
    [Fact]
    public async Task BrotliCompressor_FallbackRule_SingleByte()
    {
        var c = new BrotliCompressor();
        var data = new byte[] { 0xAB };
        var compressed = await c.CompressAsync(data);
        Assert.Null(compressed);
    }

    // 5b. ZstandardCompressor returns null for incompressible 1-byte input
    [Fact]
    public async Task ZstandardCompressor_FallbackRule_SingleByte()
    {
        var c = new ZstandardCompressor();
        var data = new byte[] { 0xAB };
        var compressed = await c.CompressAsync(data);
        Assert.Null(compressed);
    }

    // 6. LeptonJpegCompressor.CompressAsync always returns null
    [Fact]
    public async Task LeptonJpegCompressor_CompressAlwaysReturnsNull()
    {
        var c = new LeptonJpegCompressor();
        var data = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 };
        var compressed = await c.CompressAsync(data);
        Assert.Null(compressed);
    }

    // 7. CompressorRegistry.SelectForExtension returns correct methods
    [Theory]
    [InlineData(".cs", CompressionMethod.Zstandard)]
    [InlineData(".html", CompressionMethod.Brotli)]
    [InlineData(".iso", CompressionMethod.Lzma)]
    [InlineData(".jpg", CompressionMethod.None)]
    [InlineData(".unknownext", CompressionMethod.Zstandard)]
    public void Registry_SelectForExtension_KnownExtensions(string ext, CompressionMethod expected)
    {
        var registry = new CompressorRegistry();
        Assert.Equal(expected, registry.SelectForExtension(ext.AsSpan()));
    }

    // 8. SelectForExtension handles leading dot or no dot
    [Fact]
    public void Registry_SelectForExtension_WithAndWithoutLeadingDot()
    {
        var registry = new CompressorRegistry();
        Assert.Equal(registry.SelectForExtension("cs".AsSpan()),
                     registry.SelectForExtension(".cs".AsSpan()));
    }

    // 9. CompressorRegistry.Get returns correct compressor for each method
    [Theory]
    [InlineData(CompressionMethod.None)]
    [InlineData(CompressionMethod.Brotli)]
    [InlineData(CompressionMethod.Zstandard)]
    [InlineData(CompressionMethod.Lzma)]
    [InlineData(CompressionMethod.LeptonJpeg)]
    public void Registry_Get_ReturnsCorrectCompressor(CompressionMethod method)
    {
        var registry = new CompressorRegistry();
        var compressor = registry.Get(method);
        Assert.Equal(method, compressor.Method);
    }

    // 10. CompressorRegistry.Register allows overriding a compressor
    [Fact]
    public void Registry_Register_OverridesCompressor()
    {
        var registry = new CompressorRegistry();
        var custom = new NoneCompressor(); // NoneCompressor.Method == None
        registry.Register(custom);
        Assert.Same(custom, registry.Get(CompressionMethod.None));
    }
}
