using System.Buffers;
using System.IO.Compression;
using Dari.Archiver.Format;

namespace Dari.Archiver.Compression;

public sealed class BrotliCompressor : ICompressor
{
    public CompressionMethod Method => CompressionMethod.Brotli;

    public ValueTask<ReadOnlyMemory<byte>?> CompressAsync(ReadOnlyMemory<byte> input, CancellationToken ct = default)
    {
        var inputSpan = input.Span;
        int maxLen = BrotliEncoder.GetMaxCompressedLength(inputSpan.Length);
        var buf = ArrayPool<byte>.Shared.Rent(maxLen);
        try
        {
            bool ok = BrotliEncoder.TryCompress(inputSpan, buf, out int written, quality: 6, window: 22);
            if (!ok || written >= inputSpan.Length)
                return ValueTask.FromResult<ReadOnlyMemory<byte>?>(null);
            return ValueTask.FromResult<ReadOnlyMemory<byte>?>(buf[..written].ToArray());
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buf);
        }
    }

    public ValueTask DecompressAsync(ReadOnlyMemory<byte> input, ulong originalSize,
        IBufferWriter<byte> output, CancellationToken ct = default)
    {
        var dest = output.GetSpan((int)originalSize);
        bool result = BrotliDecoder.TryDecompress(input.Span, dest, out int written);
        if (!result)
            throw new InvalidDataException("Brotli decompression failed.");
        output.Advance(written);
        return ValueTask.CompletedTask;
    }
}
