using System.Buffers.Binary;
using Dari.Archiver.Diagnostics;

namespace Dari.Archiver.Format;

/// <summary>
/// Dari archive header — always located at offset 0, exactly 13 bytes (§3).
/// <code>
/// Byte  0–3   signature  "DARI"
/// Byte  4     version    5
/// Bytes 5–12  timestamp  u64 LE  (Unix epoch seconds)
/// </code>
/// </summary>
public readonly struct DariHeader
{
    public DateTimeOffset CreatedAt { get; }

    private DariHeader(DateTimeOffset createdAt) => CreatedAt = createdAt;

    /// <summary>
    /// Reads and validates a <see cref="DariHeader"/> from the first 13 bytes of <paramref name="span"/>.
    /// Throws <see cref="DariFormatException"/> on any violation.
    /// </summary>
    public static DariHeader ReadFrom(ReadOnlySpan<byte> span)
    {
        if (!span[..4].SequenceEqual(DariConstants.HeaderMagic))
            throw DariFormatException.BadHeaderMagic();

        byte version = span[4];
        if (version != DariConstants.FormatVersion)
            throw DariFormatException.UnsupportedVersion(version);

        ulong epoch = BinaryPrimitives.ReadUInt64LittleEndian(span[5..]);
        return new DariHeader(DateTimeOffset.FromUnixTimeSeconds((long)epoch));
    }

    /// <summary>Writes this header into the first 13 bytes of <paramref name="span"/>.</summary>
    public void WriteTo(Span<byte> span)
    {
        DariConstants.HeaderMagic.CopyTo(span);
        span[4] = DariConstants.FormatVersion;
        BinaryPrimitives.WriteUInt64LittleEndian(span[5..], (ulong)CreatedAt.ToUnixTimeSeconds());
    }

    /// <summary>Creates a new header stamped with the current UTC time.</summary>
    public static DariHeader CreateNew() => new(DateTimeOffset.UtcNow);

    /// <summary>Creates a header with a specific creation timestamp.</summary>
    public static DariHeader CreateWith(DateTimeOffset createdAt) => new(createdAt);
}
