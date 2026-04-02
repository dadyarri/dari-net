namespace Dari.Archiver.Format;

/// <summary>Bit-field stored in the <c>bitflags</c> field of an index entry (§6.2).</summary>
[Flags]
public enum IndexFlags : ushort
{
    None = 0,
    /// <summary>Entry is a deduplication reference; its <c>offset</c> points to the primary data block.</summary>
    LinkedData = 0x0001,
    /// <summary>The data block is encrypted with ChaCha20-Poly1305 (§9).</summary>
    EncryptedData = 0x0002,
}
