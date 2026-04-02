using Dari.Archiver.Extra;

namespace Dari.Archiver.Tests;

public sealed class ExtraFieldTests
{
    // ------------------------------------------------------------------
    // Parse
    // ------------------------------------------------------------------

    [Fact]
    public void Parse_Empty_ReturnsEmpty()
    {
        var ef = ExtraField.Parse("");
        Assert.Equal(0, ef.Count);
        Assert.Equal(string.Empty, ef.Serialize());
    }

    [Fact]
    public void Parse_SinglePair()
    {
        var ef = ExtraField.Parse("e=chacha20poly1305");
        Assert.True(ef.TryGetValue("e", out string v));
        Assert.Equal("chacha20poly1305", v);
    }

    [Fact]
    public void Parse_MultiplePairs()
    {
        var ef = ExtraField.Parse("e=chacha20poly1305;en=aabbccdd;et=1122334455667788");
        Assert.Equal(3, ef.Count);
        Assert.Equal("chacha20poly1305", ef.GetValueOrDefault("e"));
        Assert.Equal("aabbccdd", ef.GetValueOrDefault("en"));
        Assert.Equal("1122334455667788", ef.GetValueOrDefault("et"));
    }

    [Fact]
    public void Parse_DuplicateKey_LastWins()
    {
        var ef = ExtraField.Parse("k=first;k=last");
        Assert.Equal(1, ef.Count);
        Assert.Equal("last", ef.GetValueOrDefault("k"));
    }

    [Fact]
    public void Parse_EscapedSemicolon_InValue()
    {
        // value contains a real semicolon encoded as %3B
        var ef = ExtraField.Parse("note=hello%3Bworld");
        Assert.Equal("hello;world", ef.GetValueOrDefault("note"));
    }

    [Fact]
    public void Parse_EscapedSemicolon_InKey()
    {
        var ef = ExtraField.Parse("ke%3By=value");
        Assert.Equal("value", ef.GetValueOrDefault("ke;y"));
    }

    [Fact]
    public void Parse_EmptyValue_IsSkipped()
    {
        // "k=" has an empty value — must be skipped per spec
        var ef = ExtraField.Parse("k=;other=yes");
        Assert.Null(ef.GetValueOrDefault("k"));
        Assert.Equal("yes", ef.GetValueOrDefault("other"));
    }

    [Fact]
    public void Parse_SegmentWithoutEquals_IsIgnored()
    {
        var ef = ExtraField.Parse("noequals;k=v");
        Assert.Equal(1, ef.Count);
        Assert.Equal("v", ef.GetValueOrDefault("k"));
    }

    // ------------------------------------------------------------------
    // Serialize
    // ------------------------------------------------------------------

    [Fact]
    public void Serialize_RoundTrip()
    {
        const string raw = "e=chacha20poly1305;en=deadbeef";
        var ef = ExtraField.Parse(raw);
        Assert.Equal(raw, ef.Serialize());
    }

    [Fact]
    public void Serialize_EscapesSemicolonInValue()
    {
        var ef = ExtraField.Empty.With("k", "a;b");
        Assert.Equal("k=a%3Bb", ef.Serialize());
    }

    [Fact]
    public void Serialize_EscapesSemicolonInKey()
    {
        var ef = ExtraField.Empty.With("k;1", "v");
        Assert.Equal("k%3B1=v", ef.Serialize());
    }

    // ------------------------------------------------------------------
    // Immutable mutation
    // ------------------------------------------------------------------

    [Fact]
    public void With_AddsNewKey()
    {
        var ef = ExtraField.Empty.With("a", "1").With("b", "2");
        Assert.Equal("1", ef.GetValueOrDefault("a"));
        Assert.Equal("2", ef.GetValueOrDefault("b"));
    }

    [Fact]
    public void With_ReplacesExistingKey()
    {
        var ef = ExtraField.Empty.With("k", "old").With("k", "new");
        Assert.Equal(1, ef.Count);
        Assert.Equal("new", ef.GetValueOrDefault("k"));
    }

    [Fact]
    public void Without_RemovesKey()
    {
        var ef = ExtraField.Empty.With("k", "v").Without("k");
        Assert.Equal(0, ef.Count);
        Assert.Null(ef.GetValueOrDefault("k"));
    }

    [Fact]
    public void Without_NonExistentKey_IsNoOp()
    {
        var ef = ExtraField.Empty.With("k", "v");
        var ef2 = ef.Without("missing");
        Assert.Equal(ef.Serialize(), ef2.Serialize());
    }

    // ------------------------------------------------------------------
    // UTF-8 parsing
    // ------------------------------------------------------------------

    [Fact]
    public void Parse_FromUtf8Bytes()
    {
        var bytes = "e=test"u8.ToArray();
        var ef = ExtraField.Parse(bytes.AsSpan());
        Assert.Equal("test", ef.GetValueOrDefault("e"));
    }

    // ------------------------------------------------------------------
    // Well-known keys constant smoke test
    // ------------------------------------------------------------------

    [Fact]
    public void WellKnownKeys_RoundTripViaWith()
    {
        var ef = ExtraField.Empty
            .With(WellKnownExtraKeys.EncryptionAlgorithm, "chacha20poly1305")
            .With(WellKnownExtraKeys.EncryptionNonce, "aabbccddeeff00112233445566778899")
            .With(WellKnownExtraKeys.EncryptionTag, "deadbeefdeadbeefdeadbeefdeadbeef");

        Assert.Equal("chacha20poly1305", ef.GetValueOrDefault(WellKnownExtraKeys.EncryptionAlgorithm));
        Assert.Equal("aabbccddeeff00112233445566778899", ef.GetValueOrDefault(WellKnownExtraKeys.EncryptionNonce));
        Assert.Equal("deadbeefdeadbeefdeadbeefdeadbeef", ef.GetValueOrDefault(WellKnownExtraKeys.EncryptionTag));
    }
}
