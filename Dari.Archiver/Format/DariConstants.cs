namespace Dari.Archiver.Format;

internal static class DariConstants
{
    public static ReadOnlySpan<byte> HeaderMagic => "DARI"u8;
    public static ReadOnlySpan<byte> FooterMagic => "DARIEND"u8;

    public const byte FormatVersion = 5;

    public const int HeaderSize = 13;
    public const int FooterSize = 15;
    public const int IndexEntryFixedSize = 85;
    public const int MinArchiveSize = HeaderSize + FooterSize; // 28

    public const int ChecksumSize = 32;

    // Non-Unix placeholders (§6.1)
    public const uint DefaultUid = 1000;
    public const uint DefaultGid = 1000;
    public const ushort DefaultPerm = 644;

    public const string KdfContext = "dari.v1.chacha20poly1305.key";
    public const int KeySize = 32;
    public const int NonceSize = 12;
    public const int TagSize = 16;
}
