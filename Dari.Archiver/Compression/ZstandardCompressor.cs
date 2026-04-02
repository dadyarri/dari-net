using System.Buffers;
using Dari.Archiver.Format;
using ZstdSharp;

namespace Dari.Archiver.Compression;

public sealed class ZstandardCompressor : ICompressor
{
    public CompressionMethod Method => CompressionMethod.Zstandard;

    public ValueTask<ReadOnlyMemory<byte>?> CompressAsync(ReadOnlyMemory<byte> input, CancellationToken ct = default)
    {
        using var compressor = new Compressor(level: 3);
        var compressed = compressor.Wrap(input.Span);
        if (compressed.Length >= input.Length)
            return ValueTask.FromResult<ReadOnlyMemory<byte>?>(null);
        return ValueTask.FromResult<ReadOnlyMemory<byte>?>((ReadOnlyMemory<byte>)compressed.ToArray());
    }

    public ValueTask DecompressAsync(ReadOnlyMemory<byte> input, ulong originalSize,
        IBufferWriter<byte> output, CancellationToken ct = default)
    {
        using var decompressor = new Decompressor();
        var decompressed = decompressor.Unwrap(input.Span);
        var dest = output.GetSpan(decompressed.Length);
        decompressed.CopyTo(dest);
        output.Advance(decompressed.Length);
        return ValueTask.CompletedTask;
    }
}
