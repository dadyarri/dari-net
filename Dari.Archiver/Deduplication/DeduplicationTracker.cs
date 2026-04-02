using Dari.Archiver.Format;

namespace Dari.Archiver.Deduplication;

/// <summary>
/// Tracks unique content blocks by their BLAKE3 checksum to enable
/// deduplication: when the same content is added more than once, only
/// the first copy is written to the data section; subsequent entries
/// reference the first via <see cref="IndexFlags.LinkedData"/>.
/// </summary>
internal sealed class DeduplicationTracker
{
    private readonly record struct PrimaryInfo(ulong Offset, CompressionMethod Method);

    private readonly Dictionary<Blake3Hash, PrimaryInfo> _map;

    /// <param name="initialCapacity">
    ///   Expected number of unique entries; used to pre-size the dictionary.
    /// </param>
    public DeduplicationTracker(int initialCapacity = 64)
    {
        _map = new Dictionary<Blake3Hash, PrimaryInfo>(initialCapacity);
    }

    /// <summary>
    /// Number of unique content blocks registered so far.
    /// </summary>
    public int Count => _map.Count;

    /// <summary>
    /// Attempts to register <paramref name="checksum"/> as a primary (first-seen) entry.
    /// </summary>
    /// <returns>
    ///   <see langword="true"/> when this checksum is new and the data block should be written;
    ///   <see langword="false"/> when a duplicate already exists.
    /// </returns>
    public bool TryRegisterPrimary(Blake3Hash checksum, ulong dataOffset, CompressionMethod method)
    {
        return _map.TryAdd(checksum, new PrimaryInfo(dataOffset, method));
    }

    /// <summary>
    /// Checks whether a data block with <paramref name="checksum"/> has already been written.
    /// </summary>
    /// <param name="checksum">BLAKE3 checksum to look up.</param>
    /// <param name="existingOffset">Absolute byte offset of the original data block.</param>
    /// <param name="method">Compression method used for the primary data block.</param>
    public bool TryGetExisting(Blake3Hash checksum, out ulong existingOffset, out CompressionMethod method)
    {
        if (_map.TryGetValue(checksum, out var info))
        {
            existingOffset = info.Offset;
            method = info.Method;
            return true;
        }
        existingOffset = 0;
        method = CompressionMethod.None;
        return false;
    }

    /// <summary>
    /// Checks whether a data block with <paramref name="checksum"/> has already been written
    /// (offset only overload for backwards compatibility).
    /// </summary>
    public bool TryGetExisting(Blake3Hash checksum, out ulong existingOffset)
        => TryGetExisting(checksum, out existingOffset, out _);

    /// <summary>
    /// Rebuilds this tracker from an existing archive's index entries.
    /// Used by <see cref="Archiving.ArchiveAppender"/> to seed deduplication
    /// from entries already present in the archive.
    /// </summary>
    public void Seed(IEnumerable<IndexEntry> entries)
    {
        foreach (var entry in entries)
        {
            // Only primary (non-linked) entries own a data block.
            if (!entry.IsLinked)
                _map.TryAdd(entry.Checksum, new PrimaryInfo(entry.Offset, entry.Compression));
        }
    }
}
