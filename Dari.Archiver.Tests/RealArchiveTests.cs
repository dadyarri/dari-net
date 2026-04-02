using System.Buffers;
using Dari.Archiver.Compression;
using Dari.Archiver.Format;
using Dari.Archiver.IO;

namespace Dari.Archiver.Tests;

/// <summary>
/// Integration tests against the real reference archive at /mnt/dev/dari_test/archive.dar,
/// produced by the original Dari implementation.
/// All tests are skipped gracefully when the file is absent.
/// </summary>
public sealed class RealArchiveTests : IAsyncLifetime
{
    private const string ArchivePath = "/mnt/dev/dari_test/archive.dar";

    private DariReader? _reader;

    public async Task InitializeAsync()
    {
        if (!File.Exists(ArchivePath)) return;
        _reader = await DariReader.OpenAsync(ArchivePath);
    }

    public async Task DisposeAsync()
    {
        if (_reader is not null)
            await _reader.DisposeAsync();
    }

    private DariReader Reader => _reader ?? throw new Exception("Archive not available.");

    private void SkipIfMissing()
    {
        if (!File.Exists(ArchivePath))
            throw new SkipException($"Reference archive not found at {ArchivePath}");
    }

    // -----------------------------------------------------------------------
    // Header / index
    // -----------------------------------------------------------------------

    [SkippableFact]
    public void Archive_Opens_WithoutException()
    {
        SkipIfMissing();
        Assert.NotNull(Reader);
    }

    [SkippableFact]
    public void Archive_EntryCount_Is561()
    {
        SkipIfMissing();
        Assert.Equal(561, Reader.Entries.Count);
    }

    [SkippableFact]
    public void Archive_Header_TimestampIsReasonable()
    {
        SkipIfMissing();
        // Archive was created 2026-04-02 — just check it's in a plausible range.
        Assert.InRange(Reader.Header.CreatedAt.Year, 2020, 2030);
    }

    // -----------------------------------------------------------------------
    // First entry — known MP3 (uncompressed)
    // -----------------------------------------------------------------------

    [SkippableFact]
    public void FirstEntry_Path_IsKnown()
    {
        SkipIfMissing();
        Assert.Equal("Battle - Battle Approaching.mp3", Reader.Entries[0].Path);
    }

    [SkippableFact]
    public void FirstEntry_Compression_IsNone()
    {
        SkipIfMissing();
        Assert.Equal(CompressionMethod.None, Reader.Entries[0].Compression);
    }

    [SkippableFact]
    public void FirstEntry_OriginalSize_Matches()
    {
        SkipIfMissing();
        Assert.Equal(9659922UL, Reader.Entries[0].OriginalSize);
    }

    [SkippableFact]
    public void FirstEntry_Metadata_IsCorrect()
    {
        SkipIfMissing();
        var e = Reader.Entries[0];
        Assert.Equal(1000u, e.Uid);
        Assert.Equal(1000u, e.Gid);
        // Archive stores the full Unix mode including file-type bits: 0o100755 = S_IFREG|rwxr-xr-x = 33261
        Assert.Equal(33261, e.Perm);
        Assert.Equal(1734102290L, e.ModifiedAt.ToUnixTimeSeconds());
    }

    // -----------------------------------------------------------------------
    // Small uncompressed entry — checksum verification
    // -----------------------------------------------------------------------

    [SkippableFact]
    public async Task SmallEntry_Checksum_MatchesStoredValue()
    {
        SkipIfMissing();

        // 'PlannerBot/.git/description' — 73 bytes, comp=None
        var entry = Reader.Entries.First(e => e.Path == "PlannerBot/.git/description");

        Assert.Equal(CompressionMethod.None, entry.Compression);
        Assert.Equal(73UL, entry.OriginalSize);

        var block = await Reader.ReadRawBlockAsync(entry);
        var computed = Blake3Hash.Of(block.Span);

        Assert.Equal("6be58b34ccef43af2d3a9ee6bbae8865c947cf89ea9799e9012ec30a1b2b7df0",
                     computed.ToString());
        Assert.Equal(entry.Checksum, computed);
    }

    // -----------------------------------------------------------------------
    // Zstd-compressed entry — decompress and verify checksum
    // -----------------------------------------------------------------------

    [SkippableFact]
    public async Task ZstdEntry_Decompresses_AndChecksumMatches()
    {
        SkipIfMissing();

        // 'PlannerBot/.dockerignore' — 338 bytes orig, 222 bytes compressed (Zstd)
        var entry = Reader.Entries.First(e => e.Path == "PlannerBot/.dockerignore");

        Assert.Equal(CompressionMethod.Zstandard, entry.Compression);
        Assert.Equal(338UL, entry.OriginalSize);
        Assert.Equal(222UL, entry.CompressedSize);

        var block = await Reader.ReadRawBlockAsync(entry);
        Assert.Equal(222, block.Length);

        // Decompress with the registry.
        var writer = new ArrayBufferWriter<byte>((int)entry.OriginalSize);
        var compressor = CompressorRegistry.Default.Get(CompressionMethod.Zstandard);
        await compressor.DecompressAsync(block, entry.OriginalSize, writer);

        Assert.Equal((int)entry.OriginalSize, writer.WrittenCount);

        var computed = Blake3Hash.Of(writer.WrittenSpan);
        Assert.Equal(entry.Checksum, computed);
    }

    // -----------------------------------------------------------------------
    // Linked (deduplicated) entries
    // -----------------------------------------------------------------------

    [SkippableFact]
    public void LinkedEntries_Count_IsTwo()
    {
        SkipIfMissing();
        Assert.Equal(2, Reader.Entries.Count(e => e.IsLinked));
    }

    [SkippableFact]
    public void LinkedEntry_SharesOffsetWithPrimary()
    {
        SkipIfMissing();

        // 'PlannerBot/.git/logs/refs/heads/master' is a linked copy of
        // 'PlannerBot/.git/logs/HEAD' (same data offset).
        var linked = Reader.Entries.First(e =>
            e.Path == "PlannerBot/.git/logs/refs/heads/master");
        var primary = Reader.Entries.First(e =>
            e.Path == "PlannerBot/.git/logs/HEAD");

        Assert.True(linked.IsLinked);
        Assert.False(primary.IsLinked);
        Assert.Equal(primary.Offset, linked.Offset);
    }

    [SkippableFact]
    public async Task LinkedEntry_DataBlock_MatchesPrimary()
    {
        SkipIfMissing();

        var linked = Reader.Entries.First(e =>
            e.Path == "PlannerBot/.git/refs/remotes/origin/master");
        var primary = Reader.Entries.First(e =>
            e.Path == "PlannerBot/.git/refs/heads/master");

        var linkedBlock = await Reader.ReadRawBlockAsync(linked);
        var primaryBlock = await Reader.ReadRawBlockAsync(primary);

        Assert.Equal(primaryBlock.ToArray(), linkedBlock.ToArray());
    }

    // -----------------------------------------------------------------------
    // Extra fields
    // -----------------------------------------------------------------------

    [SkippableFact]
    public void EntryWithExtra_Parses_AllThreeFields()
    {
        SkipIfMissing();

        var entry = Reader.Entries.First(e => e.Path == "IMG_1830.jpg");
        var extra = entry.Extra;

        Assert.Equal(3, extra.Count);
        // Original implementation stores quoted string values (e.g. "Apple" not Apple)
        Assert.Equal("\"Apple\"", extra.GetValueOrDefault("imk"));
        Assert.Equal("\"iPhone 13\"", extra.GetValueOrDefault("imd"));
        Assert.Equal("2025-06-22 07:37:50", extra.GetValueOrDefault("idt"));
    }

    // -----------------------------------------------------------------------
    // Compression variety
    // -----------------------------------------------------------------------

    [SkippableFact]
    public void Archive_Has475NoneAnd86ZstdEntries()
    {
        SkipIfMissing();
        Assert.Equal(475, Reader.Entries.Count(e => e.Compression == CompressionMethod.None));
        Assert.Equal(86, Reader.Entries.Count(e => e.Compression == CompressionMethod.Zstandard));
    }
}
