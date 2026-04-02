using System.Text;
using Dari.Archiver.Archiving;
using Dari.Archiver.Compression;
using Dari.Archiver.Format;

namespace Dari.Archiver.Tests;

public sealed class ArchiveReaderWriterTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"dari-tests-{Guid.NewGuid():N}");

    public ArchiveReaderWriterTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private string TempFile(string name) => Path.Combine(_tempDir, name);

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static async Task<ArchiveReader> RoundTripAsync(
        Func<ArchiveWriter, Task> addFiles,
        CompressorRegistry? registry = null)
    {
        var ms = new MemoryStream();
        await using (var writer = await ArchiveWriter.CreateAsync(ms, leaveOpen: true,
                         compressors: registry))
            await addFiles(writer);
        ms.Position = 0;
        return await ArchiveReader.OpenAsync(ms, leaveOpen: false);
    }

    // -----------------------------------------------------------------------
    // Empty archive
    // -----------------------------------------------------------------------

    [Fact]
    public async Task EmptyArchive_HasNoEntries()
    {
        await using var reader = await RoundTripAsync(_ => Task.CompletedTask);
        Assert.Empty(reader.Entries);
    }

    // -----------------------------------------------------------------------
    // Single file round-trip
    // -----------------------------------------------------------------------

    [Fact]
    public async Task SingleFile_ExtractAsync_ContentMatches()
    {
        var original = Encoding.UTF8.GetBytes("Hello, Dari archive!");
        var meta = new FileMetadata(DateTimeOffset.UtcNow);

        await using var reader = await RoundTripAsync(async w =>
            await w.AddAsync(new MemoryStream(original), "hello.txt", meta));

        var entry = Assert.Single(reader.Entries);
        Assert.Equal("hello.txt", entry.Path);

        var ms = new MemoryStream();
        await reader.ExtractAsync(entry, ms);
        Assert.Equal(original, ms.ToArray());
    }

    [Fact]
    public async Task SingleFile_ChecksumIsVerified()
    {
        var data = "checksum test"u8.ToArray();
        var meta = new FileMetadata(DateTimeOffset.UtcNow);

        await using var reader = await RoundTripAsync(async w =>
            await w.AddAsync(new MemoryStream(data), "f.txt", meta));

        // Should not throw.
        var ms = new MemoryStream();
        await reader.ExtractAsync(reader.Entries[0], ms, verifyChecksum: true);
    }

    // -----------------------------------------------------------------------
    // Checksum mismatch detection
    // -----------------------------------------------------------------------

    [Fact]
    public async Task CorruptedBlock_ChecksumVerification_Throws()
    {
        var data = "important content"u8.ToArray();
        var meta = new FileMetadata(DateTimeOffset.UtcNow);

        // Build archive in memory, then corrupt one byte of the data block.
        var ms = new MemoryStream();
        await using (var writer = await ArchiveWriter.CreateAsync(ms, leaveOpen: true))
            await writer.AddAsync(new MemoryStream(data), "f.txt", meta);

        // Flip a byte inside the data block (starts at offset 13 after the header).
        ms.Position = 13;
        ms.WriteByte(0xFF);

        ms.Position = 0;
        await using var reader = await ArchiveReader.OpenAsync(ms, leaveOpen: false);
        var entry = reader.Entries[0];

        await Assert.ThrowsAsync<InvalidDataException>(async () =>
        {
            var dest = new MemoryStream();
            await reader.ExtractAsync(entry, dest, verifyChecksum: true);
        });
    }

    // -----------------------------------------------------------------------
    // Compressed entry: write via ArchiveWriter, read+decompress via ArchiveReader
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ZstdCompressedFile_ExtractedContentMatches()
    {
        // A .cs file will be compressed with Zstandard by the default registry.
        string source = string.Join("\n", Enumerable.Repeat(
            "public class Foo { public void Bar() { } }", 50));
        var data = Encoding.UTF8.GetBytes(source);
        var meta = new FileMetadata(DateTimeOffset.UtcNow);

        await using var reader = await RoundTripAsync(async w =>
            await w.AddAsync(new MemoryStream(data), "src/Foo.cs", meta));

        var entry = reader.Entries[0];
        Assert.Equal(CompressionMethod.Zstandard, entry.Compression);
        Assert.True(entry.CompressedSize < entry.OriginalSize,
            "Repetitive source code should compress smaller.");

        var ms = new MemoryStream();
        await reader.ExtractAsync(entry, ms, verifyChecksum: true);
        Assert.Equal(data, ms.ToArray());
    }

    // -----------------------------------------------------------------------
    // AddAsync from disk file
    // -----------------------------------------------------------------------

    [Fact]
    public async Task AddFromDisk_RoundTrips()
    {
        var sourceFile = TempFile("source.txt");
        await File.WriteAllTextAsync(sourceFile, "file on disk content");

        var archivePath = TempFile("out.dar");
        await using (var writer = await ArchiveWriter.CreateAsync(archivePath))
            await writer.AddAsync(sourceFile, "data/source.txt");

        await using var reader = await ArchiveReader.OpenAsync(archivePath);
        var entry = Assert.Single(reader.Entries);
        Assert.Equal("data/source.txt", entry.Path);

        var ms = new MemoryStream();
        await reader.ExtractAsync(entry, ms);
        Assert.Equal("file on disk content", Encoding.UTF8.GetString(ms.ToArray()));
    }

    [Fact]
    public async Task AddFromDisk_MissingFile_Throws()
    {
        await using var writer = await ArchiveWriter.CreateAsync(
            new MemoryStream(), leaveOpen: true);

        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            writer.AddAsync("/nonexistent/file.txt", "f.txt").AsTask());
    }

    // -----------------------------------------------------------------------
    // AddDirectoryAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task AddDirectory_AllFilesPresent()
    {
        // Create a small directory tree.
        var srcDir = TempFile("srcdir");
        Directory.CreateDirectory(Path.Combine(srcDir, "sub"));
        await File.WriteAllTextAsync(Path.Combine(srcDir, "a.txt"), "aaa");
        await File.WriteAllTextAsync(Path.Combine(srcDir, "b.txt"), "bbb");
        await File.WriteAllTextAsync(Path.Combine(srcDir, "sub", "c.txt"), "ccc");

        await using var reader = await RoundTripAsync(async w =>
            await w.AddDirectoryAsync(srcDir, archivePrefix: "tree"));

        Assert.Equal(3, reader.Entries.Count);
        var paths = reader.Entries.Select(e => e.Path).OrderBy(p => p).ToList();
        Assert.Contains("tree/a.txt", paths);
        Assert.Contains("tree/b.txt", paths);
        Assert.Contains("tree/sub/c.txt", paths);
    }

    [Fact]
    public async Task AddDirectory_ContentRoundTrips()
    {
        var srcDir = TempFile("srcdir2");
        Directory.CreateDirectory(srcDir);
        await File.WriteAllTextAsync(Path.Combine(srcDir, "hello.txt"), "hello world");

        await using var reader = await RoundTripAsync(async w =>
            await w.AddDirectoryAsync(srcDir));

        var entry = Assert.Single(reader.Entries);
        var ms = new MemoryStream();
        await reader.ExtractAsync(entry, ms);
        Assert.Equal("hello world", Encoding.UTF8.GetString(ms.ToArray()));
    }

    [Fact]
    public async Task AddDirectory_MissingDir_Throws()
    {
        await using var writer = await ArchiveWriter.CreateAsync(
            new MemoryStream(), leaveOpen: true);

        await Assert.ThrowsAsync<DirectoryNotFoundException>(() =>
            writer.AddDirectoryAsync("/no/such/dir").AsTask());
    }

    // -----------------------------------------------------------------------
    // ExtractToFileAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ExtractToFile_WritesContentAndCreatesDirectories()
    {
        var data = "extracted content"u8.ToArray();
        var meta = new FileMetadata(DateTimeOffset.UtcNow);

        await using var reader = await RoundTripAsync(async w =>
            await w.AddAsync(new MemoryStream(data), "nested/dir/file.txt", meta));

        var outPath = TempFile("extracted/nested/dir/file.txt");
        await reader.ExtractToFileAsync(reader.Entries[0], outPath);

        Assert.True(File.Exists(outPath));
        Assert.Equal(data, await File.ReadAllBytesAsync(outPath));
    }

    // -----------------------------------------------------------------------
    // ExtractAllAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ExtractAll_RecreatesDirectoryTree()
    {
        var srcDir = TempFile("srcdir3");
        Directory.CreateDirectory(Path.Combine(srcDir, "a"));
        await File.WriteAllTextAsync(Path.Combine(srcDir, "a", "x.txt"), "x content");
        await File.WriteAllTextAsync(Path.Combine(srcDir, "y.txt"), "y content");

        await using var reader = await RoundTripAsync(async w =>
            await w.AddDirectoryAsync(srcDir));

        var outDir = TempFile("extracted_all");
        await reader.ExtractAllAsync(outDir);

        Assert.Equal("x content", await File.ReadAllTextAsync(Path.Combine(outDir, "a", "x.txt")));
        Assert.Equal("y content", await File.ReadAllTextAsync(Path.Combine(outDir, "y.txt")));
    }

    // -----------------------------------------------------------------------
    // Multiple files — order preserved
    // -----------------------------------------------------------------------

    [Fact]
    public async Task MultipleFiles_OrderPreserved()
    {
        var files = new[] { "z.txt", "a.txt", "m/n.txt" };
        var meta = new FileMetadata(DateTimeOffset.UtcNow);

        await using var reader = await RoundTripAsync(async w =>
        {
            foreach (var f in files)
                await w.AddAsync(new MemoryStream([1, 2, 3]), f, meta);
        });

        Assert.Equal(files, reader.Entries.Select(e => e.Path));
    }

    // -----------------------------------------------------------------------
    // Real archive — ArchiveReader against reference file
    // -----------------------------------------------------------------------

    private const string RefArchivePath = "/mnt/dev/dari_test/archive.dar";

    [SkippableFact]
    public async Task RealArchive_ExtractSmallEntry_MatchesChecksum()
    {
        Skip.IfNot(File.Exists(RefArchivePath));

        await using var reader = await ArchiveReader.OpenAsync(RefArchivePath);

        var entry = reader.Entries.First(e => e.Path == "PlannerBot/.git/description");
        var ms = new MemoryStream();
        // ExtractAsync with verifyChecksum=true proves the full decompression+checksum pipeline.
        await reader.ExtractAsync(entry, ms, verifyChecksum: true);

        Assert.Equal((int)entry.OriginalSize, ms.Length);
    }

    [SkippableFact]
    public async Task RealArchive_ExtractZstdEntry_MatchesChecksum()
    {
        Skip.IfNot(File.Exists(RefArchivePath));

        await using var reader = await ArchiveReader.OpenAsync(RefArchivePath);

        var entry = reader.Entries.First(e => e.Path == "PlannerBot/.dockerignore");
        Assert.Equal(CompressionMethod.Zstandard, entry.Compression);

        var ms = new MemoryStream();
        await reader.ExtractAsync(entry, ms, verifyChecksum: true);
        Assert.Equal((int)entry.OriginalSize, ms.Length);
    }
}
