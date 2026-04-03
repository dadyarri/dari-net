using Dari.Archiver.Format;

namespace Dari.Archiver.Diagnostics;

/// <summary>Thrown when a <c>.dar</c> archive violates the Dari v5 format specification.</summary>
public sealed class DariFormatException : Exception
{
    public DariFormatException(string message) : base(message) { }
    public DariFormatException(string message, Exception inner) : base(message, inner) { }

    internal static DariFormatException BadHeaderMagic() =>
        new("Invalid Dari archive: header magic bytes are not 'DARI'.");

    internal static DariFormatException UnsupportedVersion(byte actual) =>
        new($"Unsupported Dari format version {actual}. Only version 5 is supported.");

    internal static DariFormatException BadFooterMagic() =>
        new("Invalid Dari archive: footer magic bytes are not 'DARIEND'.");

    internal static DariFormatException FileTooShort(long length) =>
        new($"File is too short ({length} bytes) to be a valid Dari archive (minimum {DariConstants.MinArchiveSize} bytes).");

    internal static DariFormatException BadIndexOffset(uint offset, long fileLength) =>
        new($"Footer index_offset {offset} is out of range for a file of length {fileLength}.");

    internal static DariFormatException WrongPassphrase(string entryPath, Exception inner) =>
        new($"Wrong passphrase: authentication tag mismatch for entry '{entryPath}'.", inner);
}
