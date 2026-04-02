using System.Security.Cryptography;
using Dari.Archiver.Format;

namespace Dari.Archiver.Crypto;

/// <summary>
/// Low-level ChaCha20-Poly1305 encryption primitives for the Dari format (§9).
/// Key derivation uses BLAKE3 <c>derive_key</c>; nonce is the first 12 bytes
/// of the file's BLAKE3 content checksum.
/// </summary>
internal static class DariEncryption
{
    /// <summary>
    /// Derives a 32-byte encryption key from passphrase bytes using the BLAKE3 KDF.
    /// Context string: <see cref="DariConstants.KdfContext"/>.
    /// </summary>
    /// <param name="passphraseUtf8">Raw UTF-8 bytes of the passphrase.</param>
    /// <param name="key32">Output span; must be exactly 32 bytes.</param>
    public static void DeriveKey(ReadOnlySpan<byte> passphraseUtf8, Span<byte> key32)
    {
        if (key32.Length != DariConstants.KeySize)
            throw new ArgumentException($"key32 must be {DariConstants.KeySize} bytes.", nameof(key32));

        var hash = Blake3Hash.DeriveKey(DariConstants.KdfContext, passphraseUtf8);
        hash.CopyTo(key32);
    }

    /// <summary>
    /// Extracts the 12-byte nonce from a BLAKE3 content checksum
    /// (nonce = <c>checksum[0..12]</c>, §9.2).
    /// </summary>
    /// <param name="blake3Checksum">32-byte checksum span.</param>
    /// <param name="nonce12">Output span; must be exactly 12 bytes.</param>
    public static void DeriveNonce(ReadOnlySpan<byte> blake3Checksum, Span<byte> nonce12)
    {
        if (blake3Checksum.Length < DariConstants.ChecksumSize)
            throw new ArgumentException(
                $"checksum must be {DariConstants.ChecksumSize} bytes.", nameof(blake3Checksum));
        if (nonce12.Length != DariConstants.NonceSize)
            throw new ArgumentException(
                $"nonce12 must be {DariConstants.NonceSize} bytes.", nameof(nonce12));

        blake3Checksum[..DariConstants.NonceSize].CopyTo(nonce12);
    }

    /// <summary>
    /// Encrypts <paramref name="plaintext"/> using ChaCha20-Poly1305.
    /// The 16-byte authentication tag is appended immediately after the ciphertext.
    /// </summary>
    /// <param name="key">32-byte key.</param>
    /// <param name="nonce">12-byte nonce.</param>
    /// <param name="plaintext">Data to encrypt.</param>
    /// <param name="ciphertextAndTag">
    ///   Output buffer; must be exactly <c>plaintext.Length + 16</c> bytes.
    /// </param>
    public static void Encrypt(
        ReadOnlySpan<byte> key,
        ReadOnlySpan<byte> nonce,
        ReadOnlySpan<byte> plaintext,
        Span<byte> ciphertextAndTag)
    {
        if (ciphertextAndTag.Length != plaintext.Length + DariConstants.TagSize)
            throw new ArgumentException(
                $"ciphertextAndTag must be plaintext.Length + {DariConstants.TagSize} bytes.",
                nameof(ciphertextAndTag));

        using var cipher = new ChaCha20Poly1305(key);
        cipher.Encrypt(
            nonce,
            plaintext,
            ciphertextAndTag[..plaintext.Length],
            ciphertextAndTag[plaintext.Length..]);
    }

    /// <summary>
    /// Decrypts <paramref name="ciphertextAndTag"/> using ChaCha20-Poly1305.
    /// Throws <see cref="AuthenticationTagMismatchException"/> if the tag is invalid.
    /// </summary>
    /// <param name="key">32-byte key.</param>
    /// <param name="nonce">12-byte nonce.</param>
    /// <param name="ciphertextAndTag">Ciphertext followed by the 16-byte tag.</param>
    /// <param name="plaintext">
    ///   Output buffer; must be exactly <c>ciphertextAndTag.Length - 16</c> bytes.
    /// </param>
    public static void Decrypt(
        ReadOnlySpan<byte> key,
        ReadOnlySpan<byte> nonce,
        ReadOnlySpan<byte> ciphertextAndTag,
        Span<byte> plaintext)
    {
        if (ciphertextAndTag.Length < DariConstants.TagSize)
            throw new ArgumentException(
                $"ciphertextAndTag must be at least {DariConstants.TagSize} bytes.",
                nameof(ciphertextAndTag));
        if (plaintext.Length != ciphertextAndTag.Length - DariConstants.TagSize)
            throw new ArgumentException(
                $"plaintext must be ciphertextAndTag.Length - {DariConstants.TagSize} bytes.",
                nameof(plaintext));

        int ciphertextLen = ciphertextAndTag.Length - DariConstants.TagSize;
        using var cipher = new ChaCha20Poly1305(key);
        cipher.Decrypt(
            nonce,
            ciphertextAndTag[..ciphertextLen],
            ciphertextAndTag[ciphertextLen..],
            plaintext);
    }
}
