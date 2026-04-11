using System.Text;
using Dari.App.Helpers;
using Dari.App.ViewModels;

namespace Dari.App.Tests;

public sealed class ContentClassifierTests
{
    static ContentClassifierTests() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    [Fact]
    public void ClassifyBytes_EmptyInput_ReturnsUtf8Text()
    {
        var result = ContentClassifier.ClassifyBytes([], 1024);

        Assert.Equal(ContentKind.Text, result.Kind);
        Assert.Equal("UTF-8", result.Encoding);
        Assert.False(result.Truncated);
    }

    [Fact]
    public void ClassifyBytes_NullByte_ReturnsBinary()
    {
        var result = ContentClassifier.ClassifyBytes([0x41, 0x00, 0x42], 1024);

        Assert.Equal(ContentKind.Binary, result.Kind);
        Assert.Equal("", result.Encoding);
    }

    [Fact]
    public void ClassifyBytes_ControlBytesOverTenPercent_ReturnsBinary()
    {
        var bytes = new byte[] { 0x01, 0x02, 0x03, (byte)'A', (byte)'B', (byte)'C', (byte)'D', (byte)'E', (byte)'F' };

        var result = ContentClassifier.ClassifyBytes(bytes, 1024);

        Assert.Equal(ContentKind.Binary, result.Kind);
    }

    [Fact]
    public void ClassifyBytes_ControlBytesAtTenPercent_RemainsText()
    {
        var bytes = new byte[] { 0x01, (byte)'A', (byte)'B', (byte)'C', (byte)'D', (byte)'E', (byte)'F', (byte)'G', (byte)'H', (byte)'I' };

        var result = ContentClassifier.ClassifyBytes(bytes, 1024);

        Assert.Equal(ContentKind.Text, result.Kind);
        Assert.Equal("UTF-8", result.Encoding);
    }

    [Fact]
    public void ClassifyBytes_InvalidUtf8_FallsBackToWindows1251()
    {
        var bytes = new byte[] { 0xCF, 0xF0, 0xE8, 0xE2, 0xE5, 0xF2 };

        var result = ContentClassifier.ClassifyBytes(bytes, 1024);

        Assert.Equal(ContentKind.Text, result.Kind);
        Assert.Equal("Windows-1251", result.Encoding);
        Assert.Equal("Привет", ContentClassifier.DecodeText(bytes, result.Encoding));
    }

    [Fact]
    public void ClassifyBytes_UsesPreviewSliceForTruncationAndClassification()
    {
        var bytes = new byte[] { (byte)'A', (byte)'B', (byte)'C', 0x00 };

        var result = ContentClassifier.ClassifyBytes(bytes, 3);

        Assert.True(result.Truncated);
        Assert.Equal(ContentKind.Text, result.Kind);
        Assert.Equal("UTF-8", result.Encoding);
    }

    [Theory]
    [InlineData(".md", PreviewState.Markdown)]
    [InlineData(".cs", PreviewState.Code)]
    [InlineData(".txt", PreviewState.Text)]
    [InlineData(".unknown", PreviewState.Text)]
    public void ClassifyForPreview_TextContent_UsesExtension(string extension, PreviewState expected)
    {
        ReadOnlySpan<byte> bytes = "hello"u8.ToArray();

        var state = ContentClassifier.ClassifyForPreview(bytes, extension, "file" + extension, 1024);

        Assert.Equal(expected, state);
    }

    [Fact]
    public void ClassifyForPreview_BinaryContent_ReturnsBinary()
    {
        var state = ContentClassifier.ClassifyForPreview([0x00, 0x01], ".md", "readme.md", 1024);

        Assert.Equal(PreviewState.Binary, state);
    }
}
