namespace Dari.Archiver.Extra;

/// <summary>Well-known key names for the Dari extra field (§7, §12).</summary>
public static class WellKnownExtraKeys
{
    // Encryption (§9)
    public const string EncryptionAlgorithm = "e";
    public const string EncryptionNonce = "en";
    public const string EncryptionTag = "et";

    // EXIF (§12)
    public const string ExifMake = "imk";
    public const string ExifModel = "imd";
    public const string ExifDateTime = "idt";

    // Audio tags (§12)
    public const string AudioTitle = "atl";
    public const string AudioArtist = "aar";
    public const string AudioAlbum = "aal";
    public const string AudioGenre = "agn";
}
