namespace Dari.Archiver.Ignoring;

/// <summary>
/// Determines whether a path should be excluded from an archive.
/// </summary>
public interface IIgnoreFilter
{
    /// <summary>
    /// Returns <see langword="true"/> if <paramref name="relativePath"/> should be excluded.
    /// </summary>
    /// <param name="relativePath">
    ///   Forward-slash-separated path relative to the archive root
    ///   (e.g. <c>PlannerBot/.idea/config.xml</c>).
    /// </param>
    /// <param name="isDirectory"><see langword="true"/> when the path refers to a directory.</param>
    bool ShouldIgnore(string relativePath, bool isDirectory);
}
