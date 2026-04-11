using System.Security.Cryptography;

namespace Dari.Archiver.Crypto;

/// <summary>
/// Runtime capability checks for cryptographic features used by the Dari format.
/// </summary>
public static class CryptoCapabilities
{
    /// <summary>
    /// <see langword="true"/> when the current platform supports ChaCha20-Poly1305 (the
    /// cipher used for archive encryption).  On Windows this requires Windows 10 version
    /// 1903 or later; on Linux and macOS it is always available.
    /// </summary>
    public static bool IsEncryptionSupported => ChaCha20Poly1305.IsSupported;
}
