using System.Text;
using Dari.App.ViewModels;

namespace Dari.App.Helpers;

public enum ContentKind { Text, Binary }

public record struct ClassifyResult(ContentKind Kind, string Encoding, bool Truncated);

internal static class ContentClassifier
{
    private static readonly HashSet<string> CodeExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs", ".fs", ".vb",
        ".xml", ".axaml", ".csproj", ".slnx", ".props", ".targets", ".xaml",
        ".json", ".toml", ".yaml", ".yml",
        ".py", ".rs", ".go",
        ".js", ".mjs", ".ts",
        ".sh", ".bash", ".fish",
        ".c", ".cpp", ".h",
        ".java", ".kt",
        ".rb", ".php",
        ".html", ".css",
        ".sql", ".dart", ".swift", ".zig",
    };

    /// <summary>
    /// Classifies <paramref name="bytes"/> as text or binary.
    /// Exact port of Rust <c>classify_bytes</c> from <c>src/tui/preview.rs</c>.
    /// </summary>
    public static ClassifyResult ClassifyBytes(ReadOnlySpan<byte> bytes, int maxBytes)
    {
        bool truncated = bytes.Length > maxBytes;
        var preview = truncated ? bytes[..maxBytes] : bytes;

        // 1. Null byte → Binary
        if (preview.IndexOf((byte)0x00) >= 0)
            return new ClassifyResult(ContentKind.Binary, "", truncated);

        // 2. Control-byte ratio > 10 % → Binary
        //    Allowed: TAB (0x09), LF (0x0A), VT (0x0B), FF (0x0C), CR (0x0D)
        int ctrl = 0;
        foreach (var b in preview)
            if (b < 0x09 || (b > 0x0D && b < 0x20))
                ctrl++;
        if (preview.Length > 0 && ctrl * 10 > preview.Length)
            return new ClassifyResult(ContentKind.Binary, "", truncated);

        // 3. Strict UTF-8
        try
        {
            Encoding.GetEncoding("UTF-8",
                EncoderFallback.ExceptionFallback,
                DecoderFallback.ExceptionFallback)
                .GetString(preview);
            return new ClassifyResult(ContentKind.Text, "UTF-8", truncated);
        }
        catch (DecoderFallbackException) { }

        // 4. Windows-1251 fallback
        try
        {
            Encoding.GetEncoding(1251,
                EncoderFallback.ExceptionFallback,
                DecoderFallback.ExceptionFallback)
                .GetString(preview);
            return new ClassifyResult(ContentKind.Text, "Windows-1251", truncated);
        }
        catch (DecoderFallbackException) { }

        return new ClassifyResult(ContentKind.Binary, "", truncated);
    }

    /// <summary>
    /// Classifies <paramref name="bytes"/> and routes to the correct <see cref="PreviewState"/>
    /// based on content kind and file extension.
    /// </summary>
    public static PreviewState ClassifyForPreview(
        ReadOnlySpan<byte> bytes, string extension, int maxBytes)
    {
        var result = ClassifyBytes(bytes, maxBytes);

        if (result.Kind == ContentKind.Binary)
            return PreviewState.Binary;

        var ext = extension.ToLowerInvariant();
        if (ext == ".md") return PreviewState.Markdown;
        if (CodeExtensions.Contains(ext)) return PreviewState.Code;
        return PreviewState.Text;
    }

    /// <summary>
    /// Decodes <paramref name="bytes"/> using the encoding name returned by
    /// <see cref="ClassifyBytes"/>. Only call after classification confirms the encoding.
    /// </summary>
    public static string DecodeText(ReadOnlySpan<byte> bytes, string encoding) =>
        encoding == "Windows-1251"
            ? Encoding.GetEncoding(1251).GetString(bytes)
            : Encoding.UTF8.GetString(bytes);
}
