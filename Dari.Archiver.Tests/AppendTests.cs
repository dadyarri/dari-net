using System.Text;
using Dari.Archiver.Archiving;
using Dari.Archiver.Format;

namespace Dari.Archiver.Tests;

/// <summary>Tests for Phase 9: ArchiveAppender.</summary>
public sealed class AppendTests : IDisposable
{
    private readonly string _tempDir;

    public AppendTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"dari-append-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    private string TempFile(string name) => Path.Combine(_tempDir, name);

    // -----------------------------------------------------------------------
    // Basic append
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Append_AddsNewEntry_EntryCountIncreases()
    {
        string archive = TempFile("test.dar");
        var meta = new FileMetadata(DateTimeOffset.UtcNow, 0, 0, 33188);

        // Create initial archive with one entry.
        await using (var writer = await ArchiveWriter.CreateAsync(archive))
            await writer.AddAsync("original.txt", "original content"u8.ToArray(), meta);

        // Append a second entry.
        await using (var appender = await ArchiveAppender.OpenAsync(archive))
            await appender.AddAsync("appended.txt", "appended content"u8.ToArray(), meta);

        using var reader = await ArchiveReader.OpenAsync(archive);
        Assert.Equal(2, reader.Entries.Count);
        Assert.Equal("original.txt", reader.Entries[0].Path);
        Assert.Equal("appended.txt", reader.Entries[1].Path);
    }

    [Fact]
    public async Task Append_ExistingContentIntact_AfterAppend()
    {
        string archive = TempFile("intact.dar");
        byte[] originalContent = Encoding.UTF8.GetBytes("original file content");
        var meta = new FileMetadata(DateTimeOffset.UtcNow, 0, 0, 33188);

        await using (var writer = await ArchiveWriter.CreateAsync(archive))
            await writer.AddAsync("orig.txt", new ReadOnlyMemory<byte>(originalContent), meta);

        await using (var appender = await ArchiveAppender.OpenAsync(archive))
            await appender.AddAsync("new.txt", "new content"u8.ToArray(), meta);

        using var reader = await ArchiveReader.OpenAsync(archive);
        var out1 = new MemoryStream();
        await reader.ExtractAsync(reader.Entries[0], out1);
        Assert.Equal(originalContent, out1.ToArray());
    }

    [Fact]
    public async Task Append_NewEntryExtractable()
    {
        string archive = TempFile("extract-new.dar");
        byte[] newContent = Encoding.UTF8.GetBytes("newly appended data");
        var meta = new FileMetadata(DateTimeOffset.UtcNow, 0, 0, 33188);

        await using (var writer = await ArchiveWriter.CreateAsync(archive))
            await writer.AddAsync("existing.txt", "existing"u8.ToArray(), meta);

        await using (var appender = await ArchiveAppender.OpenAsync(archive))
            await appender.AddAsync("new.txt", new ReadOnlyMemory<byte>(newContent), meta);

        using var reader = await ArchiveReader.OpenAsync(archive);
        var outNew = new MemoryStream();
        await reader.ExtractAsync(reader.Entries[1], outNew);
        Assert.Equal(newContent, outNew.ToArray());
    }

    [Fact]
    public async Task Append_ExistingEntriesProperty_ReflectsOriginalIndex()
    {
        string archive = TempFile("existing-entries.dar");
        var meta = new FileMetadata(DateTimeOffset.UtcNow, 0, 0, 33188);

        await using (var writer = await ArchiveWriter.CreateAsync(archive))
        {
            await writer.AddAsync("a.txt", "A"u8.ToArray(), meta);
            await writer.AddAsync("b.txt", "B"u8.ToArray(), meta);
        }

        await using var appender = await ArchiveAppender.OpenAsync(archive);
        Assert.Equal(2, appender.ExistingEntries.Count);
        Assert.Equal("a.txt", appender.ExistingEntries[0].Path);
        Assert.Equal("b.txt", appender.ExistingEntries[1].Path);
        await appender.AddAsync("c.txt", "C"u8.ToArray(), meta);
    }

    // -----------------------------------------------------------------------
    // Deduplication across append boundary
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Append_DuplicateOfExistingContent_EmitsLinkedEntry()
    {
        string archive = TempFile("dedup-append.dar");
        byte[] sharedContent = Encoding.UTF8.GetBytes("shared data block");
        var meta = new FileMetadata(DateTimeOffset.UtcNow, 0, 0, 33188);

        await using (var writer = await ArchiveWriter.CreateAsync(archive))
            await writer.AddAsync("primary.txt", new ReadOnlyMemory<byte>(sharedContent), meta);

        // Append the same content — should become a linked entry.
        await using (var appender = await ArchiveAppender.OpenAsync(archive))
            await appender.AddAsync("linked.txt", new ReadOnlyMemory<byte>(sharedContent), meta);

        using var reader = await ArchiveReader.OpenAsync(archive);
        Assert.Equal(2, reader.Entries.Count);
        Assert.False(reader.Entries[0].IsLinked);
        Assert.True(reader.Entries[1].IsLinked);
        Assert.Equal(reader.Entries[0].Offset, reader.Entries[1].Offset);
    }

    [Fact]
    public async Task Append_LinkedEntryFromExisting_StillExtractable()
    {
        string archive = TempFile("dedup-extract.dar");
        byte[] sharedContent = Encoding.UTF8.GetBytes("content that will be deduped");
        var meta = new FileMetadata(DateTimeOffset.UtcNow, 0, 0, 33188);

        await using (var writer = await ArchiveWriter.CreateAsync(archive))
            await writer.AddAsync("primary.txt", new ReadOnlyMemory<byte>(sharedContent), meta);

        await using (var appender = await ArchiveAppender.OpenAsync(archive))
            await appender.AddAsync("linked.txt", new ReadOnlyMemory<byte>(sharedContent), meta);

        using var reader = await ArchiveReader.OpenAsync(archive);
        var out1 = new MemoryStream();
        var out2 = new MemoryStream();
        await reader.ExtractAsync(reader.Entries[0], out1);
        await reader.ExtractAsync(reader.Entries[1], out2);

        Assert.Equal(sharedContent, out1.ToArray());
        Assert.Equal(sharedContent, out2.ToArray());
    }

    // -----------------------------------------------------------------------
    // Multiple appends
    // -----------------------------------------------------------------------

    [Fact]
    public async Task MultipleAppends_AllEntriesPresent()
    {
        string archive = TempFile("multi-append.dar");
        var meta = new FileMetadata(DateTimeOffset.UtcNow, 0, 0, 33188);

        await using (var w = await ArchiveWriter.CreateAsync(archive))
            await w.AddAsync("1.txt", "one"u8.ToArray(), meta);

        for (int i = 2; i <= 5; i++)
        {
            await using var app = await ArchiveAppender.OpenAsync(archive);
            await app.AddAsync($"{i}.txt", Encoding.UTF8.GetBytes($"content {i}"), meta);
        }

        using var reader = await ArchiveReader.OpenAsync(archive);
        Assert.Equal(5, reader.Entries.Count);
        for (int i = 1; i <= 5; i++)
            Assert.Contains(reader.Entries, e => e.Path == $"{i}.txt");
    }

    // -----------------------------------------------------------------------
    // AddAsync from disk file
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Append_FromDiskFile_Works()
    {
        string archive = TempFile("from-disk.dar");
        string srcFile = TempFile("source.txt");
        await File.WriteAllTextAsync(srcFile, "disk file content");
        var meta = new FileMetadata(DateTimeOffset.UtcNow, 0, 0, 33188);

        await using (var w = await ArchiveWriter.CreateAsync(archive))
            await w.AddAsync("seed.txt", "seed"u8.ToArray(), meta);

        await using (var app = await ArchiveAppender.OpenAsync(archive))
            await app.AddAsync(srcFile, "source.txt");

        using var reader = await ArchiveReader.OpenAsync(archive);
        Assert.Equal(2, reader.Entries.Count);

        var outMs = new MemoryStream();
        await reader.ExtractAsync(reader.Entries[1], outMs);
        Assert.Equal("disk file content", Encoding.UTF8.GetString(outMs.ToArray()));
    }
}
