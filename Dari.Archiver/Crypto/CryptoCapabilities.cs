using System.Security.Cryptography;

namespace Dari.Archiver.Crypto;

/// <summary>
/// Runtime capability checks for cryptographic features used by the Dari format.
/// </summary>
public static class CryptoCapabilities
{
    /// <summary>
    /// <see langword="true"/> when the current platform supports ChaCha20-Poly1305 (the
    /// cipher used for archive encryption).  On Windows, the underlying CNG provider does
    /// not expose ChaCha20-Poly1305 reliably regardless of OS version, so this always
    /// returns <see langword="false"/> on Windows.  On Linux and macOS it uses OpenSSL
    /// and is always available.
    /// </summary>
    public static bool IsEncryptionSupported =>
        !OperatingSystem.IsWindows() && ChaCha20Poly1305.IsSupported;
}
