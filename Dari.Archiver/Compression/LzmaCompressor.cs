using System.Buffers;
using Dari.Archiver.Format;
using SharpCompress.Compressors.LZMA;

namespace Dari.Archiver.Compression;

public sealed class LzmaCompressor : ICompressor
{
    public CompressionMethod Method => CompressionMethod.Lzma;

    public ValueTask<ReadOnlyMemory<byte>?> CompressAsync(ReadOnlyMemory<byte> input, CancellationToken ct = default)
    {
        var ms = new MemoryStream();
        byte[] props;
        // eos=false: decoder will use the stored originalSize; avoids writing EOS marker
        using (var enc = LzmaStream.Create(new LzmaEncoderProperties(false), false, ms))
        {
            var inputArray = input.ToArray();
            enc.Write(inputArray, 0, inputArray.Length);
            props = enc.Properties; // 5-byte LZMA properties
        }

        var compressedData = ms.ToArray();
        // Format: [5 bytes props] + [compressed data]
        var result = new byte[5 + compressedData.Length];
        props.CopyTo(result, 0);
        compressedData.CopyTo(result, 5);

        if (result.Length >= input.Length)
            return ValueTask.FromResult<ReadOnlyMemory<byte>?>(null);

        return ValueTask.FromResult<ReadOnlyMemory<byte>?>((ReadOnlyMemory<byte>)result);
    }

    public ValueTask DecompressAsync(ReadOnlyMemory<byte> input, ulong originalSize,
        IBufferWriter<byte> output, CancellationToken ct = default)
    {
        if (input.Length < 5)
            throw new InvalidDataException("LZMA data too short: missing properties header.");

        var props = input.Span[..5].ToArray();
        var compressedData = input.Slice(5).ToArray();
        var compressedStream = new MemoryStream(compressedData);

        using var dec = LzmaStream.Create(props, compressedStream, -1, (long)originalSize, false);
        var dest = output.GetMemory((int)originalSize);
        var destStream = new MemoryStream2(dest);
        dec.CopyTo(destStream);
        output.Advance((int)originalSize);
        return ValueTask.CompletedTask;
    }

    // Minimal write-only Stream over a Memory<byte> for CopyTo destination
    private sealed class MemoryStream2 : Stream
    {
        private readonly Memory<byte> _buffer;
        private int _position;

        public MemoryStream2(Memory<byte> buffer) => _buffer = buffer;

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => _buffer.Length;
        public override long Position
        {
            get => _position;
            set => throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            buffer.AsSpan(offset, count).CopyTo(_buffer.Span[_position..]);
            _position += count;
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            buffer.CopyTo(_buffer.Span[_position..]);
            _position += buffer.Length;
        }

        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }
}
