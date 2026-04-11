namespace Dari.App.Services;

/// <summary>
/// Manages the list of recently opened archive files.
/// Persisted to <c>recent.json</c> alongside the application config file.
/// </summary>
public interface IRecentFilesService
{
    /// <summary>Maximum number of recent files to keep.</summary>
    const int MaxEntries = 10;

    /// <summary>Returns the current list of recent file paths (most recent first).</summary>
    IReadOnlyList<string> Load();

    /// <summary>
    /// Adds <paramref name="path"/> to the top of the list (deduplicating if already present)
    /// and persists the updated list.
    /// </summary>
    void Add(string path);

    /// <summary>Removes <paramref name="path"/> from the list and persists the updated list.</summary>
    void Remove(string path);

    /// <summary>Clears all recent files and persists an empty list.</summary>
    void Clear();
}
