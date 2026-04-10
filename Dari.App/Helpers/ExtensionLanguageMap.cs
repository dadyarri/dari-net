namespace Dari.App.Helpers;

internal static class ExtensionLanguageMap
{
    public static string? GetScopeByExtension(string extension) =>
        extension.ToLowerInvariant() switch
        {
            ".cs" => "source.cs",
            ".fs" => "source.fsharp",
            ".vb" => "source.asp.vb.net",

            ".xml" or ".axaml" or ".csproj" or ".slnx" or ".props" or ".targets" or ".xaml" => "text.xml",

            ".json" => "source.json",
            ".toml" => "source.toml",
            ".yaml" or ".yml" => "source.yaml",

            ".py" => "source.python",
            ".rs" => "source.rust",
            ".go" => "source.go",

            ".js" or ".mjs" => "source.js",
            ".ts" => "source.ts",

            ".sh" or ".bash" or ".fish" => "source.shell",

            ".c" or ".h" => "source.c",
            ".cpp" => "source.cpp",
            ".java" => "source.java",
            ".kt" => "source.kotlin",

            ".rb" => "source.ruby",
            ".php" => "source.php",
            ".html" => "text.html.basic",
            ".css" => "source.css",

            ".sql" => "source.sql",
            ".dart" => "source.dart",
            ".swift" => "source.swift",
            ".zig" => "source.zig",
            _ => null,
        };
}
