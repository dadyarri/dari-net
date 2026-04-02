using System.Buffers.Binary;
using System.Runtime.InteropServices;
using Blake3;

namespace Dari.Archiver.Format;

/// <summary>
/// A 32-byte BLAKE3 hash stored as a value type.
/// Equality is byte-wise; <see cref="GetHashCode"/> uses the first 8 bytes
/// for O(1) dictionary lookups.
/// </summary>
/// <remarks>
/// Use <see cref="Of(ReadOnlySpan{byte})"/> to compute a hash and
/// <see cref="DeriveKey"/> to derive a key via the BLAKE3 KDF.
/// The constructor accepts pre-computed raw bytes (e.g. read from an index entry).
/// </remarks>
[StructLayout(LayoutKind.Sequential, Size = 32)]
public readonly struct Blake3Hash : IEquatable<Blake3Hash>
{
    private readonly ulong _a;
    private readonly ulong _b;
    private readonly ulong _c;
    private readonly ulong _d;

    /// <summary>Creates a <see cref="Blake3Hash"/> by copying exactly 32 raw bytes.</summary>
    public Blake3Hash(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 32)
            throw new ArgumentException("BLAKE3 hash must be exactly 32 bytes.", nameof(bytes));

        _a = BinaryPrimitives.ReadUInt64LittleEndian(bytes);
        _b = BinaryPrimitives.ReadUInt64LittleEndian(bytes[8..]);
        _c = BinaryPrimitives.ReadUInt64LittleEndian(bytes[16..]);
        _d = BinaryPrimitives.ReadUInt64LittleEndian(bytes[24..]);
    }

    // -----------------------------------------------------------------------
    // Computation
    // -----------------------------------------------------------------------

    /// <summary>Computes the BLAKE3 hash of <paramref name="data"/>.</summary>
    public static Blake3Hash Of(ReadOnlySpan<byte> data)
    {
        Span<byte> buf = stackalloc byte[32];
        Hasher.Hash(data, buf);
        return new Blake3Hash(buf);
    }

    /// <summary>
    /// Derives a 32-byte key using the BLAKE3 KDF:
    /// <c>blake3_derive_key(context, inputKeyMaterial)</c>.
    /// </summary>
    /// <param name="context">
    ///   Application-specific context string (e.g. <see cref="DariConstants.KdfContext"/>).
    ///   Must be a compile-time constant per the BLAKE3 spec.
    /// </param>
    /// <param name="inputKeyMaterial">Key material (e.g. passphrase bytes).</param>
    public static Blake3Hash DeriveKey(string context, ReadOnlySpan<byte> inputKeyMaterial)
    {
        Span<byte> buf = stackalloc byte[32];
        using var hasher = Hasher.NewDeriveKey(context);
        hasher.Update(inputKeyMaterial);
        hasher.Finalize(buf);
        return new Blake3Hash(buf);
    }

    // -----------------------------------------------------------------------
    // Output helpers
    // -----------------------------------------------------------------------

    /// <summary>Writes the 32 hash bytes into <paramref name="destination"/>.</summary>
    public void CopyTo(Span<byte> destination)
    {
        if (destination.Length < 32)
            throw new ArgumentException("Destination must be at least 32 bytes.", nameof(destination));

        BinaryPrimitives.WriteUInt64LittleEndian(destination, _a);
        BinaryPrimitives.WriteUInt64LittleEndian(destination[8..], _b);
        BinaryPrimitives.WriteUInt64LittleEndian(destination[16..], _c);
        BinaryPrimitives.WriteUInt64LittleEndian(destination[24..], _d);
    }

    /// <summary>Returns the first 12 bytes as a nonce (used for ChaCha20-Poly1305, §9.3).</summary>
    public void CopyNonceTo(Span<byte> nonce12)
    {
        if (nonce12.Length < 12)
            throw new ArgumentException("Nonce span must be at least 12 bytes.", nameof(nonce12));

        BinaryPrimitives.WriteUInt64LittleEndian(nonce12, _a);
        BinaryPrimitives.WriteUInt32LittleEndian(nonce12[8..], (uint)(_b & 0xFFFF_FFFF));
    }

    // -----------------------------------------------------------------------
    // Equality / formatting
    // -----------------------------------------------------------------------

    public bool Equals(Blake3Hash other) =>
        _a == other._a && _b == other._b && _c == other._c && _d == other._d;

    public override bool Equals(object? obj) => obj is Blake3Hash h && Equals(h);

    // Use the first 8 bytes as the hash code — already high-entropy for BLAKE3 output.
    public override int GetHashCode() => HashCode.Combine(_a, _b);

    public static bool operator ==(Blake3Hash left, Blake3Hash right) => left.Equals(right);
    public static bool operator !=(Blake3Hash left, Blake3Hash right) => !left.Equals(right);

    public override string ToString()
    {
        Span<byte> buf = stackalloc byte[32];
        CopyTo(buf);
        return Convert.ToHexStringLower(buf);
    }
}
