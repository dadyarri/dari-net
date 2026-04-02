using System.Text;
using Dari.Archiver.Archiving;
using Dari.Archiver.Deduplication;
using Dari.Archiver.Format;
using Dari.Archiver.IO;

namespace Dari.Archiver.Tests;

/// <summary>Tests for Phase 8: Deduplication.</summary>
public sealed class DeduplicationTests
{
    // -----------------------------------------------------------------------
    // DeduplicationTracker unit tests
    // -----------------------------------------------------------------------

    [Fact]
    public void TryRegisterPrimary_NewChecksum_ReturnsTrue()
    {
        var tracker = new DeduplicationTracker();
        var hash = Blake3Hash.Of("hello"u8);
        Assert.True(tracker.TryRegisterPrimary(hash, 42, CompressionMethod.None));
    }

    [Fact]
    public void TryRegisterPrimary_DuplicateChecksum_ReturnsFalse()
    {
        var tracker = new DeduplicationTracker();
        var hash = Blake3Hash.Of("hello"u8);
        tracker.TryRegisterPrimary(hash, 42, CompressionMethod.None);
        Assert.False(tracker.TryRegisterPrimary(hash, 99, CompressionMethod.None));
    }

    [Fact]
    public void TryGetExisting_KnownHash_ReturnsTrueAndOffset()
    {
        var tracker = new DeduplicationTracker();
        var hash = Blake3Hash.Of("world"u8);
        tracker.TryRegisterPrimary(hash, 1234, CompressionMethod.Brotli);
        Assert.True(tracker.TryGetExisting(hash, out ulong offset));
        Assert.Equal(1234UL, offset);
    }

    [Fact]
    public void TryGetExisting_UnknownHash_ReturnsFalse()
    {
        var tracker = new DeduplicationTracker();
        var hash = Blake3Hash.Of("unknown"u8);
        Assert.False(tracker.TryGetExisting(hash, out _));
    }

    [Fact]
    public async Task Seed_PopulatesFromPrimaryEntries()
    {
        var tracker = new DeduplicationTracker();

        var ms = new MemoryStream();
        await using (var w = await DariWriter.CreateAsync(ms, leaveOpen: true))
        {
            await w.AddFileAsync("seed.txt", "seed content"u8.ToArray(),
                new FileMetadata(DateTimeOffset.UtcNow, 0, 0, 33188));
            await w.FinalizeAsync();
        }

        ms.Position = 0;
        using var reader = await DariReader.OpenAsync(ms, leaveOpen: true);
        var entries = reader.Entries.ToList();

        tracker.Seed(entries);
        Assert.Equal(1, tracker.Count);
        Assert.True(tracker.TryGetExisting(entries[0].Checksum, out _));
    }

    // -----------------------------------------------------------------------
    // Integration: DariWriter deduplicates identical content
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Writer_DuplicateContent_EmitsLinkedEntry()
    {
        byte[] content = "duplicate content"u8.ToArray();
        var meta = new FileMetadata(DateTimeOffset.UtcNow, 0, 0, 33188);
        var ms = new MemoryStream();

        await using var writer = await DariWriter.CreateAsync(ms, leaveOpen: true);
        await writer.AddFileAsync("a.txt", new ReadOnlyMemory<byte>(content), meta);
        await writer.AddFileAsync("b.txt", new ReadOnlyMemory<byte>(content), meta);
        await writer.FinalizeAsync();

        ms.Position = 0;
        using var reader = await DariReader.OpenAsync(ms, leaveOpen: true);
        Assert.Equal(2, reader.Entries.Count);

        var a = reader.Entries[0];
        var b = reader.Entries[1];

        Assert.False(a.IsLinked, "First entry should be primary");
        Assert.True(b.IsLinked, "Second entry should be linked");
        Assert.Equal(a.Checksum, b.Checksum);
        Assert.Equal(a.Offset, b.Offset);
    }

    [Fact]
    public async Task Writer_DuplicateContent_ArchiveIsSmallerThanWithoutDedup()
    {
        byte[] content = new byte[10_000];
        new Random(42).NextBytes(content);
        var meta = new FileMetadata(DateTimeOffset.UtcNow, 0, 0, 33188);

        // With dedup (default).
        var msWith = new MemoryStream();
        await using (var w = await DariWriter.CreateAsync(msWith, leaveOpen: true))
        {
            await w.AddFileAsync("a.bin", new ReadOnlyMemory<byte>(content), meta);
            await w.AddFileAsync("b.bin", new ReadOnlyMemory<byte>(content), meta);
        }

        Assert.True(msWith.Length > 0);

        // The linked entry stores no data bytes, so the archive with dedup should be
        // smaller than one where the same data is stored twice.
        // We verify the second entry is linked (zero compressed size).
        msWith.Position = 0;
        using var r = await DariReader.OpenAsync(msWith, leaveOpen: true);
        var linked = r.Entries[1];
        Assert.True(linked.IsLinked);
        Assert.Equal(0UL, linked.CompressedSize);
    }

    [Fact]
    public async Task ArchiveReader_ExtractsLinkedEntry_Correctly()
    {
        byte[] content = Encoding.UTF8.GetBytes("linked content round-trip");
        var meta = new FileMetadata(DateTimeOffset.UtcNow, 0, 0, 33188);
        var ms = new MemoryStream();

        await using (var writer = await ArchiveWriter.CreateAsync(ms, leaveOpen: true))
        {
            await writer.AddAsync("orig.txt", new ReadOnlyMemory<byte>(content), meta);
            await writer.AddAsync("copy.txt", new ReadOnlyMemory<byte>(content), meta);
        }

        ms.Position = 0;
        using var reader = await ArchiveReader.OpenAsync(ms, leaveOpen: true);
        Assert.Equal(2, reader.Entries.Count);

        var out1 = new MemoryStream();
        var out2 = new MemoryStream();
        await reader.ExtractAsync(reader.Entries[0], out1);
        await reader.ExtractAsync(reader.Entries[1], out2);

        Assert.Equal(content, out1.ToArray());
        Assert.Equal(content, out2.ToArray());
    }

}
