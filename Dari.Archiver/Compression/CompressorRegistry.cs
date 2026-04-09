using System.Collections.Frozen;
using Dari.Archiver.Format;

namespace Dari.Archiver.Compression;

public sealed class CompressorRegistry
{
    public static CompressorRegistry Default { get; } = new CompressorRegistry();

    private readonly Dictionary<CompressionMethod, ICompressor> _compressors;
    private readonly FrozenDictionary<string, CompressionMethod> _extensionMap;
    private readonly CompressionMethod? _forcedMethod;

    public CompressorRegistry()
    {
        _compressors = new Dictionary<CompressionMethod, ICompressor>
        {
            [CompressionMethod.None] = new NoneCompressor(),
            [CompressionMethod.Brotli] = new BrotliCompressor(),
            [CompressionMethod.Zstandard] = new ZstandardCompressor(),
            [CompressionMethod.Lzma] = new LzmaCompressor(),
            [CompressionMethod.LeptonJpeg] = new LeptonJpegCompressor(),
        };
        _extensionMap = BuildExtensionMap();
    }

    private CompressorRegistry(CompressionMethod forced) : this()
    {
        _forcedMethod = forced;
    }

    /// <summary>
    /// Creates a registry that always selects <paramref name="method"/> regardless of file extension.
    /// Useful when the user explicitly chooses a compression algorithm.
    /// </summary>
    public static CompressorRegistry CreateFixed(CompressionMethod method) => new(method);

    public CompressionMethod SelectForExtension(ReadOnlySpan<char> extension)
    {
        if (_forcedMethod.HasValue) return _forcedMethod.Value;

        var ext = extension.StartsWith(".") ? extension[1..] : extension;
        var key = ext.ToString().ToLowerInvariant();
        return _extensionMap.TryGetValue(key, out var method) ? method : CompressionMethod.Zstandard;
    }

    public ICompressor Get(CompressionMethod method) =>
        _compressors.TryGetValue(method, out var c) ? c
            : throw new ArgumentOutOfRangeException(nameof(method), $"No compressor for {method}.");

    public void Register(ICompressor compressor) =>
        _compressors[compressor.Method] = compressor;

    private static FrozenDictionary<string, CompressionMethod> BuildExtensionMap()
    {
        var map = new Dictionary<string, CompressionMethod>(StringComparer.OrdinalIgnoreCase);

        // None (store raw) — already-compressed or high-entropy formats
        foreach (var ext in new[]
        {
            "jpg","jpeg","png","webp","gif",
            "mp4","mp3","aac","ogg","flac","wav","mkv","avi","mov","m4a","m4v",
            "zip","gz","rar","7z","bz2","zst","tar","bz","xz","lzma","lz4","lz","zlib",
            "jar","war","ear","apk","ipa","aab",
            "whl","egg","nupkg","gem",
            "pdf","docx","xlsx","pptx","odt","ods","odp","epub","cbz",
            "wasm",
        })
            map[ext] = CompressionMethod.None;

        // Brotli — text/web formats
        foreach (var ext in new[]
        {
            "html","htm","xhtml","css","scss","sass","less","stylus",
            "js","mjs","jsx","ts","tsx","mts",
            "json","svg","xml","xsl","xsd",
            "txt","md","markdown","rst","toml","yaml","yml",
            "woff2",
        })
            map[ext] = CompressionMethod.Brotli;

        // Zstandard — source code and structured data (also the default for unknowns)
        foreach (var ext in new[]
        {
            "log","csv","tsv","db","sql","bak",
            "rs","go","java","kt","py","rb","php","pl","pas",
            "c","cpp","c++","h","hpp","cs","fs","vb","vba",
            "sh","bat","ps1","fish",
            "proto","thrift",
        })
            map[ext] = CompressionMethod.Zstandard;

        // LZMA — binary and specialised formats
        foreach (var ext in new[]
        {
            "iso","img","bin","deb","rpm","pkg","vmdk","patch","diff",
            "fortran","f90","ada","lisp","scm","hs","erl",
            "cmake","makefile","mk","tex","bib",
        })
            map[ext] = CompressionMethod.Lzma;

        return map.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    }
}
