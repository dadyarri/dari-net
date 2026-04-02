namespace Dari.Archiver.Format;

/// <summary>Compression algorithm stored in an index entry (§6.3).</summary>
public enum CompressionMethod : byte
{
    None = 0,
    Brotli = 1,
    Zstandard = 2,
    Lzma = 3,
    LeptonJpeg = 4,
}
