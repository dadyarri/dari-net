using System.Security.Cryptography;
using System.Text;
using Dari.Archiver.Archiving;
using Dari.Archiver.Crypto;
using Dari.Archiver.Diagnostics;
using Dari.Archiver.Extra;
using Dari.Archiver.Format;
using Dari.Archiver.IO;

namespace Dari.Archiver.Tests;

/// <summary>Tests for Phase 7 encryption (ChaCha20-Poly1305).</summary>
public sealed class EncryptionTests
{
    // -----------------------------------------------------------------------
    // DariEncryption primitives
    // -----------------------------------------------------------------------

    [Fact]
    public void DeriveKey_ProducesDeterministicKey()
    {
        using var a = new DariPassphrase("secret");
        using var b = new DariPassphrase("secret");

        Span<byte> ka = stackalloc byte[32];
        Span<byte> kb = stackalloc byte[32];
        a.DeriveKey(ka);
        b.DeriveKey(kb);

        Assert.True(ka.SequenceEqual(kb));
    }

    [Fact]
    public void DeriveKey_DifferentPassphrase_ProducesDifferentKey()
    {
        using var a = new DariPassphrase("secret1");
        using var b = new DariPassphrase("secret2");

        Span<byte> ka = stackalloc byte[32];
        Span<byte> kb = stackalloc byte[32];
        a.DeriveKey(ka);
        b.DeriveKey(kb);

        Assert.False(ka.SequenceEqual(kb));
    }

    [Fact]
    public void DeriveNonce_ProducesFirst12BytesOfInput()
    {
        Span<byte> checksumBytes = stackalloc byte[32];
        for (int i = 0; i < 32; i++) checksumBytes[i] = (byte)(i + 1);

        Span<byte> nonce = stackalloc byte[12];
        DariEncryption.DeriveNonce(checksumBytes, nonce);

        Assert.True(nonce.SequenceEqual(checksumBytes[..12]));
    }

    [Fact]
    public void EncryptDecrypt_RoundTrip()
    {
        Span<byte> key = stackalloc byte[32];
        RandomNumberGenerator.Fill(key);
        Span<byte> nonce = stackalloc byte[12];
        RandomNumberGenerator.Fill(nonce);

        byte[] plaintext = Encoding.UTF8.GetBytes("Hello, Dari encryption!");
        byte[] ciphertextAndTag = new byte[plaintext.Length + 16];
        DariEncryption.Encrypt(key, nonce, plaintext, ciphertextAndTag);

        byte[] recovered = new byte[plaintext.Length];
        DariEncryption.Decrypt(key, nonce, ciphertextAndTag, recovered);

        Assert.Equal(plaintext, recovered);
    }

    [Fact]
    public void Decrypt_WrongKey_Throws()
    {
        byte[] key = new byte[32];
        byte[] nonce = new byte[12];
        RandomNumberGenerator.Fill(key);
        RandomNumberGenerator.Fill(nonce);

        byte[] plaintext = Encoding.UTF8.GetBytes("secret data");
        byte[] ciphertextAndTag = new byte[plaintext.Length + 16];
        DariEncryption.Encrypt(key, nonce, plaintext, ciphertextAndTag);

        // Corrupt the key.
        key[0] ^= 0xFF;
        byte[] recovered = new byte[plaintext.Length];

        Assert.Throws<AuthenticationTagMismatchException>(() =>
            DariEncryption.Decrypt(key, nonce, ciphertextAndTag, recovered));
    }

    // -----------------------------------------------------------------------
    // DariWriter + DariReader encrypted round-trip
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Writer_EncryptedEntry_SetsEncryptedFlag()
    {
        using var passphrase = new DariPassphrase("mypass");
        var ms = new MemoryStream();
        await using var writer = await DariWriter.CreateAsync(ms, leaveOpen: true, passphrase: passphrase);

        byte[] content = "hello world"u8.ToArray();
        var meta = new FileMetadata(DateTimeOffset.UtcNow, 0, 0, 33188);
        await writer.AddFileAsync("test.txt", new ReadOnlyMemory<byte>(content), meta);
        await writer.FinalizeAsync();

        ms.Position = 0;
        using var reader = await DariReader.OpenAsync(ms, leaveOpen: true);
        Assert.Single(reader.Entries);
        Assert.True(reader.Entries[0].IsEncrypted);
    }

    [Fact]
    public async Task Writer_EncryptedEntry_HasEncryptionExtraFields()
    {
        using var passphrase = new DariPassphrase("mypass");
        var ms = new MemoryStream();
        await using var writer = await DariWriter.CreateAsync(ms, leaveOpen: true, passphrase: passphrase);

        byte[] content = "hello world"u8.ToArray();
        var meta = new FileMetadata(DateTimeOffset.UtcNow, 0, 0, 33188);
        await writer.AddFileAsync("test.txt", new ReadOnlyMemory<byte>(content), meta);
        await writer.FinalizeAsync();

        ms.Position = 0;
        using var reader = await DariReader.OpenAsync(ms, leaveOpen: true);
        var entry = reader.Entries[0];

        Assert.Equal("chacha20poly1305", entry.Extra.GetValueOrDefault(WellKnownExtraKeys.EncryptionAlgorithm));
        string? nonceHex = entry.Extra.GetValueOrDefault(WellKnownExtraKeys.EncryptionNonce);
        string? tagHex = entry.Extra.GetValueOrDefault(WellKnownExtraKeys.EncryptionTag);
        Assert.NotNull(nonceHex);
        Assert.NotNull(tagHex);
        Assert.Equal(24, nonceHex.Length);  // 12 bytes → 24 hex chars
        Assert.Equal(32, tagHex.Length);    // 16 bytes → 32 hex chars
    }

    [Fact]
    public async Task ArchiveReader_DecryptsCorrectly_RoundTrip()
    {
        using var passphrase = new DariPassphrase("correct-horse-battery-staple");
        byte[] content = Encoding.UTF8.GetBytes("The quick brown fox jumps over the lazy dog.");
        var meta = new FileMetadata(DateTimeOffset.UtcNow, 1000, 1000, 33188);

        var ms = new MemoryStream();
        await using (var writer = await ArchiveWriter.CreateAsync(ms, leaveOpen: true, passphrase: passphrase))
        {
            await writer.AddAsync("fox.txt", new ReadOnlyMemory<byte>(content), meta);
        }

        ms.Position = 0;
        using var reader = await ArchiveReader.OpenAsync(ms, leaveOpen: true, passphrase: passphrase);
        Assert.Single(reader.Entries);

        var outMs = new MemoryStream();
        await reader.ExtractAsync(reader.Entries[0], outMs);
        Assert.Equal(content, outMs.ToArray());
    }

    [Fact]
    public async Task ArchiveReader_WrongPassphrase_ThrowsDariFormatException()
    {
        using var writePass = new DariPassphrase("correct");
        byte[] content = "secret content"u8.ToArray();
        var meta = new FileMetadata(DateTimeOffset.UtcNow, 0, 0, 33188);

        var ms = new MemoryStream();
        await using (var writer = await ArchiveWriter.CreateAsync(ms, leaveOpen: true, passphrase: writePass))
        {
            await writer.AddAsync("secret.txt", new ReadOnlyMemory<byte>(content), meta);
        }

        ms.Position = 0;
        using var wrongPass = new DariPassphrase("wrong");
        using var reader = await ArchiveReader.OpenAsync(ms, leaveOpen: true, passphrase: wrongPass);
        var outMs = new MemoryStream();

        var ex = await Assert.ThrowsAsync<DariFormatException>(
            () => reader.ExtractAsync(reader.Entries[0], outMs).AsTask());
        Assert.Contains("Wrong passphrase", ex.Message);
        Assert.Contains("secret.txt", ex.Message);
        Assert.IsType<AuthenticationTagMismatchException>(ex.InnerException);
    }

    [Fact]
    public async Task ArchiveReader_NoPassphrase_ThrowsInvalidOperation()
    {
        using var writePass = new DariPassphrase("secret");
        byte[] content = "secret content"u8.ToArray();
        var meta = new FileMetadata(DateTimeOffset.UtcNow, 0, 0, 33188);

        var ms = new MemoryStream();
        await using (var writer = await ArchiveWriter.CreateAsync(ms, leaveOpen: true, passphrase: writePass))
        {
            await writer.AddAsync("secret.txt", new ReadOnlyMemory<byte>(content), meta);
        }

        ms.Position = 0;
        using var reader = await ArchiveReader.OpenAsync(ms, leaveOpen: true, passphrase: null);
        var outMs = new MemoryStream();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => reader.ExtractAsync(reader.Entries[0], outMs).AsTask());
    }

    [Fact]
    public async Task EncryptedArchive_MultipleEntries_AllDecryptCorrectly()
    {
        using var passphrase = new DariPassphrase("multi-entry-pass");
        var entries = new (string path, string content)[]
        {
            ("a.txt", "Alpha content"),
            ("b.txt", "Beta content with more text"),
            ("c.txt", "Gamma content 12345"),
        };
        var meta = new FileMetadata(DateTimeOffset.UtcNow, 0, 0, 33188);

        var ms = new MemoryStream();
        await using (var writer = await ArchiveWriter.CreateAsync(ms, leaveOpen: true, passphrase: passphrase))
        {
            foreach (var (path, content) in entries)
                await writer.AddAsync(path, new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes(content)), meta);
        }

        ms.Position = 0;
        using var reader = await ArchiveReader.OpenAsync(ms, leaveOpen: true, passphrase: passphrase);
        Assert.Equal(3, reader.Entries.Count);

        for (int i = 0; i < entries.Length; i++)
        {
            var outMs = new MemoryStream();
            await reader.ExtractAsync(reader.Entries[i], outMs);
            Assert.Equal(entries[i].content, Encoding.UTF8.GetString(outMs.ToArray()));
        }
    }

    [Fact]
    public async Task EncryptedAndCompressed_RoundTrip()
    {
        // Highly compressible content to ensure compression kicks in.
        using var passphrase = new DariPassphrase("compress-and-encrypt");
        string repeated = string.Concat(Enumerable.Repeat("AAABBBCCC", 500));
        byte[] content = Encoding.UTF8.GetBytes(repeated);
        var meta = new FileMetadata(DateTimeOffset.UtcNow, 0, 0, 33188);

        var ms = new MemoryStream();
        await using (var writer = await ArchiveWriter.CreateAsync(ms, leaveOpen: true, passphrase: passphrase))
        {
            // Use .zst extension to trigger Zstandard compression.
            await writer.AddAsync("data.zst", new ReadOnlyMemory<byte>(content), meta);
        }

        ms.Position = 0;
        using var reader = await ArchiveReader.OpenAsync(ms, leaveOpen: true, passphrase: passphrase);
        Assert.Single(reader.Entries);
        Assert.True(reader.Entries[0].IsEncrypted);

        var outMs = new MemoryStream();
        await reader.ExtractAsync(reader.Entries[0], outMs);
        Assert.Equal(content, outMs.ToArray());
    }

    [Fact]
    public async Task UnencryptedArchive_ReadsWithoutPassphrase()
    {
        byte[] content = "plain text"u8.ToArray();
        var meta = new FileMetadata(DateTimeOffset.UtcNow, 0, 0, 33188);

        var ms = new MemoryStream();
        await using (var writer = await ArchiveWriter.CreateAsync(ms, leaveOpen: true))
        {
            await writer.AddAsync("plain.txt", new ReadOnlyMemory<byte>(content), meta);
        }

        ms.Position = 0;
        using var reader = await ArchiveReader.OpenAsync(ms, leaveOpen: true, passphrase: null);
        Assert.Single(reader.Entries);
        Assert.False(reader.Entries[0].IsEncrypted);

        var outMs = new MemoryStream();
        await reader.ExtractAsync(reader.Entries[0], outMs);
        Assert.Equal(content, outMs.ToArray());
    }

    [Fact]
    public void DariPassphrase_DisposeClearsMemory()
    {
        var pass = new DariPassphrase("zeroize-me");
        Span<byte> key1 = stackalloc byte[32];
        pass.DeriveKey(key1);
        Assert.NotEqual(0, key1[0] | key1[1] | key1[2]);  // sanity: not all zeros

        pass.Dispose();
        // After dispose, DeriveKey should throw ObjectDisposedException.
        Assert.Throws<ObjectDisposedException>(() =>
        {
            Span<byte> key2 = stackalloc byte[32];
            pass.DeriveKey(key2);
        });
    }

    // -----------------------------------------------------------------------
    // VerifyPassphraseAsync
    // -----------------------------------------------------------------------

    [Fact]
    public async Task VerifyPassphrase_CorrectPassphrase_ReturnsTrue()
    {
        using var writePass = new DariPassphrase("correct");
        byte[] content = "verify me"u8.ToArray();
        var meta = new FileMetadata(DateTimeOffset.UtcNow, 0, 0, 33188);

        var ms = new MemoryStream();
        await using (var writer = await ArchiveWriter.CreateAsync(ms, leaveOpen: true, passphrase: writePass))
            await writer.AddAsync("f.txt", new ReadOnlyMemory<byte>(content), meta);

        ms.Position = 0;
        using var reader = await ArchiveReader.OpenAsync(ms, leaveOpen: true);
        using var pass = new DariPassphrase("correct");
        Assert.True(await reader.VerifyPassphraseAsync(pass));
    }

    [Fact]
    public async Task VerifyPassphrase_WrongPassphrase_ReturnsFalse()
    {
        using var writePass = new DariPassphrase("correct");
        byte[] content = "verify me"u8.ToArray();
        var meta = new FileMetadata(DateTimeOffset.UtcNow, 0, 0, 33188);

        var ms = new MemoryStream();
        await using (var writer = await ArchiveWriter.CreateAsync(ms, leaveOpen: true, passphrase: writePass))
            await writer.AddAsync("f.txt", new ReadOnlyMemory<byte>(content), meta);

        ms.Position = 0;
        using var reader = await ArchiveReader.OpenAsync(ms, leaveOpen: true);
        using var wrong = new DariPassphrase("wrong");
        Assert.False(await reader.VerifyPassphraseAsync(wrong));
    }

    [Fact]
    public async Task VerifyPassphrase_NoEncryptedEntries_ReturnsTrue()
    {
        byte[] content = "plain"u8.ToArray();
        var meta = new FileMetadata(DateTimeOffset.UtcNow, 0, 0, 33188);

        var ms = new MemoryStream();
        await using (var writer = await ArchiveWriter.CreateAsync(ms, leaveOpen: true))
            await writer.AddAsync("f.txt", new ReadOnlyMemory<byte>(content), meta);

        ms.Position = 0;
        using var reader = await ArchiveReader.OpenAsync(ms, leaveOpen: true);
        using var anyPass = new DariPassphrase("irrelevant");
        Assert.True(await reader.VerifyPassphraseAsync(anyPass));
    }
}
