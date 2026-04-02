using Dari.Archiver.Archiving;

namespace Dari.Archiver.Ignoring;

/// <summary>
/// Loads <c>.darignore</c> and <c>.gitignore</c> files hierarchically from a
/// directory tree and exposes them as an <see cref="IIgnoreFilter"/>.
/// </summary>
/// <remarks>
/// Rules cascade exactly as git does: each directory's ignore file applies to
/// all paths beneath that directory, and later rules override earlier ones.
/// Use <see cref="Load"/> to build the filter by walking the tree once upfront,
/// or use it incrementally inside <see cref="ArchiveWriter.AddDirectoryAsync"/>
/// where rules are gathered per-directory during the walk.
/// </remarks>
public sealed class GitIgnoreFilter : IIgnoreFilter
{
    /// <summary>
    /// Names of ignore files that are loaded, in priority order
    /// (<c>.darignore</c> overrides <c>.gitignore</c>).
    /// </summary>
    public static readonly string[] IgnoreFileNames = [".darignore", ".gitignore"];

    // Each entry maps a relative directory prefix (e.g. "PlannerBot") to
    // an Ignore instance loaded from the ignore files in that directory.
    private readonly List<(string relDir, global::Ignore.Ignore ignore)> _layers = [];

    private GitIgnoreFilter() { }

    /// <summary>
    /// Walks <paramref name="rootDirectory"/> recursively, loads every
    /// <c>.darignore</c> / <c>.gitignore</c> found, and returns a filter
    /// ready for use.
    /// </summary>
    public static GitIgnoreFilter Load(string rootDirectory)
    {
        var filter = new GitIgnoreFilter();
        filter.AddDirectory(rootDirectory, rootDirectory, "");
        return filter;
    }

    // Recursively discover ignore files, building relative paths as we go.
    private void AddDirectory(string rootDir, string currentDir, string relDir)
    {
        var ig = TryLoadIgnoreFiles(currentDir, relDir);
        if (ig is not null) _layers.Add((relDir, ig));

        foreach (string subDir in Directory.EnumerateDirectories(currentDir))
        {
            string name = Path.GetFileName(subDir);
            string childRel = relDir.Length > 0 ? $"{relDir}/{name}" : name;
            AddDirectory(rootDir, subDir, childRel);
        }
    }

    /// <inheritdoc/>
    public bool ShouldIgnore(string relativePath, bool isDirectory)
    {
        // Walk layers from most-specific (deepest) to root so later rules win.
        // A path is only tested against layers whose relDir is a prefix of the path.
        bool ignored = false;

        foreach (var (relDir, ig) in _layers)
        {
            // Only apply this layer's rules to paths inside its directory.
            string prefix = relDir.Length > 0 ? relDir + "/" : "";
            if (!relativePath.StartsWith(prefix, StringComparison.Ordinal) && relDir.Length > 0)
                continue;

            // The Ignore library expects paths relative to the ignore file's directory.
            string localPath = relativePath[prefix.Length..];
            if (localPath.Length == 0) continue;

            if (isDirectory && !localPath.EndsWith('/'))
                localPath += "/";

            if (ig.IsIgnored(localPath))
                ignored = true;
        }

        return ignored;
    }

    /// <summary>
    /// Adds rules from ignore files found in <paramref name="directory"/>.
    /// Called per-directory during <see cref="ArchiveWriter.AddDirectoryAsync"/> incremental walk.
    /// </summary>
    internal static global::Ignore.Ignore? TryLoadIgnoreFiles(string directory, string relDir)
    {
        global::Ignore.Ignore? ig = null;

        foreach (string name in IgnoreFileNames)
        {
            string path = Path.Combine(directory, name);
            if (!File.Exists(path)) continue;

            ig ??= new global::Ignore.Ignore();
            foreach (string line in File.ReadLines(path))
                ig.Add(line);
        }

        return ig;
    }
}
