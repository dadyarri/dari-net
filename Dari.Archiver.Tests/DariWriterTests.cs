using System.Buffers;
using System.Text;
using Dari.Archiver.Compression;
using Dari.Archiver.Extra;
using Dari.Archiver.Format;
using Dari.Archiver.IO;

namespace Dari.Archiver.Tests;

public sealed class DariWriterTests
{
    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static async Task<DariReader> RoundTripAsync(
        Func<DariWriter, Task> writeAction)
    {
        var ms = new MemoryStream();
        await using (var writer = await DariWriter.CreateAsync(ms, leaveOpen: true))
        {
            await writeAction(writer);
            await writer.FinalizeAsync();
        }
        ms.Position = 0;
        return await DariReader.OpenAsync(ms, leaveOpen: false);
    }

    private static FileMetadata TestMeta(string mtime = "2024-01-01T00:00:00Z") =>
        new(DateTimeOffset.Parse(mtime));

    // -----------------------------------------------------------------------
    // Empty archive
    // -----------------------------------------------------------------------

    [Fact]
    public async Task EmptyArchive_IsValidAndHasNoEntries()
    {
        using var reader = await RoundTripAsync(_ => Task.CompletedTask);

        Assert.Empty(reader.Entries);
    }

    [Fact]
    public async Task EmptyArchive_HeaderTimestamp_IsRecent()
    {
        var before = DateTimeOffset.UtcNow.AddSeconds(-2);
        using var reader = await RoundTripAsync(_ => Task.CompletedTask);
        var after = DateTimeOffset.UtcNow.AddSeconds(2);

        Assert.InRange(reader.Header.CreatedAt, before, after);
    }

    // -----------------------------------------------------------------------
    // Single file
    // -----------------------------------------------------------------------

    [Fact]
    public async Task SingleFile_EntryCount_IsOne()
    {
        using var reader = await RoundTripAsync(async w =>
            await w.AddFileAsync("hello.txt", "Hello, world!"u8.ToArray(), TestMeta()));

        Assert.Single(reader.Entries);
    }

    [Fact]
    public async Task SingleFile_Path_RoundTrips()
    {
        using var reader = await RoundTripAsync(async w =>
            await w.AddFileAsync("docs/readme.md", "# Readme"u8.ToArray(), TestMeta()));

        Assert.Equal("docs/readme.md", reader.Entries[0].Path);
    }

    [Fact]
    public async Task SingleFile_Metadata_RoundTrips()
    {
        var meta = new FileMetadata(
            modifiedAt: new DateTimeOffset(2023, 6, 15, 12, 0, 0, TimeSpan.Zero),
            uid: 1001, gid: 1002, perm: 0b_110_100_100); // 0644 octal

        using var reader = await RoundTripAsync(async w =>
            await w.AddFileAsync("file.txt", "data"u8.ToArray(), meta));

        var entry = reader.Entries[0];
        Assert.Equal(meta.ModifiedAt.ToUnixTimeSeconds(),
                     entry.ModifiedAt.ToUnixTimeSeconds());
        Assert.Equal(1001u, entry.Uid);
        Assert.Equal(1002u, entry.Gid);
        Assert.Equal(0b_110_100_100, entry.Perm); // 0644 octal
    }

    [Fact]
    public async Task SingleFile_OriginalSize_IsRecorded()
    {
        var data = "Hello!"u8.ToArray();
        using var reader = await RoundTripAsync(async w =>
            await w.AddFileAsync("f.txt", data, TestMeta()));

        Assert.Equal((ulong)data.Length, reader.Entries[0].OriginalSize);
    }

    [Fact]
    public async Task SingleFile_DataBlock_CanBeReadBack()
    {
        var original = "Round-trip test content"u8.ToArray();

        using var reader = await RoundTripAsync(async w =>
        {
            // Force None compression so we can read raw bytes directly.
            var reg = new CompressorRegistry();
            reg.Register(new AlwaysNoneCompressor());
            await w.AddFileAsync("raw.bin", original, TestMeta(), registry: reg);
        });

        var block = await reader.ReadRawBlockAsync(reader.Entries[0]);
        Assert.Equal(original, block.ToArray());
    }

    // -----------------------------------------------------------------------
    // Multiple files
    // -----------------------------------------------------------------------

    [Fact]
    public async Task MultipleFiles_EntryOrder_IsPreserved()
    {
        var paths = new[] { "a.txt", "b/c.txt", "d/e/f.txt" };

        using var reader = await RoundTripAsync(async w =>
        {
            foreach (var p in paths)
                await w.AddFileAsync(p, Encoding.UTF8.GetBytes(p), TestMeta());
        });

        Assert.Equal(paths, reader.Entries.Select(e => e.Path));
    }

    [Fact]
    public async Task MultipleFiles_DataBlocks_DoNotOverlap()
    {
        using var reader = await RoundTripAsync(async w =>
        {
            var reg = new CompressorRegistry();
            reg.Register(new AlwaysNoneCompressor());
            for (int i = 0; i < 5; i++)
                await w.AddFileAsync($"file{i}.bin",
                    Encoding.UTF8.GetBytes($"content {i}"), TestMeta(), registry: reg);
        });

        // Each entry's offset must be >= the previous entry's end.
        for (int i = 1; i < reader.Entries.Count; i++)
        {
            var prev = reader.Entries[i - 1];
            var curr = reader.Entries[i];
            Assert.True(curr.Offset >= prev.Offset + prev.CompressedSize,
                $"Entry {i} offset {curr.Offset} overlaps with entry {i - 1}");
        }
    }

    // -----------------------------------------------------------------------
    // Compression fallback
    // -----------------------------------------------------------------------

    [Fact]
    public async Task CompressionFallback_StoresRawAndSetsMethodNone()
    {
        // Incompressible random data → compressor should return null → raw stored.
        var rng = new Random(42);
        var noise = new byte[256];
        rng.NextBytes(noise);

        // Use Brotli on incompressible binary data; it will fallback to None.
        var reg = new CompressorRegistry();
        reg.Register(new AlwaysNullCompressor(CompressionMethod.Brotli));

        using var reader = await RoundTripAsync(async w =>
            await w.AddFileAsync("noise.bin", noise, TestMeta(), registry: reg));

        var entry = reader.Entries[0];
        Assert.Equal(CompressionMethod.None, entry.Compression);
        Assert.Equal((ulong)noise.Length, entry.CompressedSize);
    }

    // -----------------------------------------------------------------------
    // Stream overload
    // -----------------------------------------------------------------------

    [Fact]
    public async Task StreamOverload_ReadsAllContent()
    {
        var data = "stream data"u8.ToArray();
        using var ms = new MemoryStream(data);

        using var reader = await RoundTripAsync(async w =>
        {
            var reg = new CompressorRegistry();
            reg.Register(new AlwaysNoneCompressor());
            await w.AddFileAsync("s.txt", ms, TestMeta(), registry: reg);
        });

        var block = await reader.ReadRawBlockAsync(reader.Entries[0]);
        Assert.Equal(data, block.ToArray());
    }

    // -----------------------------------------------------------------------
    // Extra fields
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ExtraFields_RoundTrip()
    {
        var extra = ExtraField.Empty
            .With(WellKnownExtraKeys.EncryptionAlgorithm, "none")
            .With("custom", "value");

        using var reader = await RoundTripAsync(async w =>
            await w.AddFileAsync("f.txt", "x"u8.ToArray(), TestMeta(), extra: extra));

        var stored = reader.Entries[0].Extra;
        Assert.Equal("none", stored.GetValueOrDefault(WellKnownExtraKeys.EncryptionAlgorithm));
        Assert.Equal("value", stored.GetValueOrDefault("custom"));
    }

    // -----------------------------------------------------------------------
    // Error handling
    // -----------------------------------------------------------------------

    [Fact]
    public async Task AddAfterFinalize_Throws()
    {
        var ms = new MemoryStream();
        await using var writer = await DariWriter.CreateAsync(ms, leaveOpen: true);
        await writer.FinalizeAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            writer.AddFileAsync("x.txt", "x"u8.ToArray(), TestMeta()).AsTask());
    }

    [Fact]
    public async Task FinalizeCalledTwice_Throws()
    {
        var ms = new MemoryStream();
        await using var writer = await DariWriter.CreateAsync(ms, leaveOpen: true);
        await writer.FinalizeAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            writer.FinalizeAsync().AsTask());
    }

    // -----------------------------------------------------------------------
    // Test-only compressor stubs
    // -----------------------------------------------------------------------

    /// <summary>Compressor that always falls back (returns null) for a given method.</summary>
    private sealed class AlwaysNullCompressor(CompressionMethod method) : ICompressor
    {
        public CompressionMethod Method => method;

        public ValueTask<ReadOnlyMemory<byte>?> CompressAsync(
            ReadOnlyMemory<byte> input, CancellationToken ct = default) =>
            ValueTask.FromResult<ReadOnlyMemory<byte>?>(null);

        public ValueTask DecompressAsync(
            ReadOnlyMemory<byte> input, ulong originalSize,
            IBufferWriter<byte> output, CancellationToken ct = default)
        {
            input.Span.CopyTo(output.GetSpan((int)originalSize));
            output.Advance(input.Length);
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>Compressor that always stores raw bytes (None method).</summary>
    private sealed class AlwaysNoneCompressor : ICompressor
    {
        public CompressionMethod Method => CompressionMethod.None;

        public ValueTask<ReadOnlyMemory<byte>?> CompressAsync(
            ReadOnlyMemory<byte> input, CancellationToken ct = default) =>
            ValueTask.FromResult<ReadOnlyMemory<byte>?>(input);

        public ValueTask DecompressAsync(
            ReadOnlyMemory<byte> input, ulong originalSize,
            IBufferWriter<byte> output, CancellationToken ct = default)
        {
            input.Span.CopyTo(output.GetSpan((int)originalSize));
            output.Advance(input.Length);
            return ValueTask.CompletedTask;
        }
    }
}
