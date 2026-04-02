using System.Buffers;

namespace Dari.Archiver.IO;

/// <summary>
/// Low-level stream reading helpers used by <see cref="DariReader"/> and <c>DariWriter</c>.
/// </summary>
internal static class BinaryHelpers
{
    /// <summary>
    /// Reads exactly <paramref name="count"/> bytes from <paramref name="stream"/> into a
    /// buffer rented from <see cref="ArrayPool{T}.Shared"/>.
    /// </summary>
    /// <remarks>
    /// The caller is responsible for returning the rented buffer via
    /// <c>ArrayPool&lt;byte&gt;.Shared.Return(buffer)</c>. The returned slice is
    /// <c>buffer[0..count]</c>.
    /// </remarks>
    public static async ValueTask<byte[]> ReadExactPooledAsync(
        Stream stream, int count, CancellationToken ct)
    {
        var buf = ArrayPool<byte>.Shared.Rent(count);
        try
        {
            await stream.ReadExactlyAsync(buf.AsMemory(0, count), ct).ConfigureAwait(false);
            return buf;
        }
        catch
        {
            ArrayPool<byte>.Shared.Return(buf);
            throw;
        }
    }

    /// <summary>
    /// Reads exactly <paramref name="count"/> bytes from <paramref name="stream"/> into a
    /// newly allocated <see cref="byte"/> array.
    /// </summary>
    public static async ValueTask<byte[]> ReadExactAsync(
        Stream stream, int count, CancellationToken ct)
    {
        var buf = new byte[count];
        await stream.ReadExactlyAsync(buf, ct).ConfigureAwait(false);
        return buf;
    }
}
