using System.Buffers;
using Dari.Archiver.Format;

namespace Dari.Archiver.Compression;

public interface ICompressor
{
    CompressionMethod Method { get; }

    // Compress input. Returns null if output >= input (fallback to raw per §8.2).
    ValueTask<ReadOnlyMemory<byte>?> CompressAsync(
        ReadOnlyMemory<byte> input, CancellationToken ct = default);

    // Decompress input into output writer. originalSize is from the index entry.
    ValueTask DecompressAsync(
        ReadOnlyMemory<byte> input, ulong originalSize,
        IBufferWriter<byte> output, CancellationToken ct = default);
}
