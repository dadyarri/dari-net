using System.Buffers;
using Dari.Archiver.Format;

namespace Dari.Archiver.Compression;

public sealed class NoneCompressor : ICompressor
{
    public CompressionMethod Method => CompressionMethod.None;

    public ValueTask<ReadOnlyMemory<byte>?> CompressAsync(ReadOnlyMemory<byte> input, CancellationToken ct = default)
        => ValueTask.FromResult<ReadOnlyMemory<byte>?>(input);

    public ValueTask DecompressAsync(ReadOnlyMemory<byte> input, ulong originalSize,
        IBufferWriter<byte> output, CancellationToken ct = default)
    {
        var dest = output.GetSpan((int)originalSize);
        input.Span.CopyTo(dest);
        output.Advance((int)originalSize);
        return ValueTask.CompletedTask;
    }
}
