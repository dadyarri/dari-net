using System.Security.Cryptography;
using System.Text;

namespace Dari.Archiver.Crypto;

/// <summary>
/// Securely holds a passphrase for Dari archive encryption/decryption.
/// The passphrase is stored as UTF-8 bytes and zeroed from memory on <see cref="Dispose"/>.
/// </summary>
/// <remarks>
/// Always wrap in a <c>using</c> block or <c>using</c> declaration to ensure the passphrase
/// bytes are zeroed when no longer needed:
/// <code>
/// using var pass = new DariPassphrase("my secret");
/// await writer.AddAsync(stream, "f.txt", meta, pass);
/// </code>
/// </remarks>
public sealed class DariPassphrase : IDisposable
{
    private byte[] _utf8Bytes;
    private bool _disposed;

    /// <summary>Creates a <see cref="DariPassphrase"/> from a <see cref="string"/>.</summary>
    public DariPassphrase(string passphrase)
    {
        ArgumentNullException.ThrowIfNull(passphrase);
        _utf8Bytes = Encoding.UTF8.GetBytes(passphrase);
    }

    /// <summary>Creates a <see cref="DariPassphrase"/> from a <see cref="ReadOnlySpan{T}"/> of chars.</summary>
    public DariPassphrase(ReadOnlySpan<char> passphrase)
    {
        _utf8Bytes = Encoding.UTF8.GetBytes(passphrase.ToArray());
    }

    /// <summary>
    /// Derives a 32-byte encryption key into <paramref name="key32"/>.
    /// </summary>
    /// <param name="key32">Caller-supplied span of exactly 32 bytes.</param>
    internal void DeriveKey(Span<byte> key32)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        DariEncryption.DeriveKey(_utf8Bytes, key32);
    }

    /// <summary>
    /// Zeros the passphrase bytes in memory.
    /// After this call the <see cref="DariPassphrase"/> must not be used.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        CryptographicOperations.ZeroMemory(_utf8Bytes);
        _utf8Bytes = [];
    }
}
