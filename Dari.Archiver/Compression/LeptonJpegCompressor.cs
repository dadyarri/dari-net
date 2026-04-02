using System.Buffers;
using Dari.Archiver.Format;

namespace Dari.Archiver.Compression;

public sealed class LeptonJpegCompressor : ICompressor
{
    public CompressionMethod Method => CompressionMethod.LeptonJpeg;

    // No managed Lepton library available; writer stores raw bytes instead.
    public ValueTask<ReadOnlyMemory<byte>?> CompressAsync(ReadOnlyMemory<byte> input, CancellationToken ct = default)
        => ValueTask.FromResult<ReadOnlyMemory<byte>?>(null);

    // Stored bytes are already the final form; pass through unchanged.
    public ValueTask DecompressAsync(ReadOnlyMemory<byte> input, ulong originalSize,
        IBufferWriter<byte> output, CancellationToken ct = default)
    {
        var dest = output.GetSpan(input.Length);
        input.Span.CopyTo(dest);
        output.Advance(input.Length);
        return ValueTask.CompletedTask;
    }
}
