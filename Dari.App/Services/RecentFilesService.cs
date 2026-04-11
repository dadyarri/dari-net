using System.Runtime.InteropServices;
using System.Text.Json;

namespace Dari.App.Services;

/// <summary>
/// Reads and writes the recent-files list as a JSON string array to a file next to the config.
/// <list type="bullet">
///   <item>Linux / macOS — <c>~/.config/dari/recent.json</c></item>
///   <item>Windows — <c>%LOCALAPPDATA%\dari\recent.json</c></item>
/// </list>
/// </summary>
public sealed class RecentFilesService : IRecentFilesService
{
    private static readonly JsonSerializerOptions s_opts = new() { WriteIndented = true };

    /// <summary>Absolute path to the recent-files file on the current platform.</summary>
    public static string FilePath { get; } = BuildPath();

    public IReadOnlyList<string> Load()
    {
        if (!File.Exists(FilePath))
            return [];

        try
        {
            string json = File.ReadAllText(FilePath);
            var list = JsonSerializer.Deserialize<List<string>>(json);
            return list ?? [];
        }
        catch
        {
            return [];
        }
    }

    public void Add(string path)
    {
        var list = new List<string>(Load());
        list.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
        list.Insert(0, path);
        if (list.Count > IRecentFilesService.MaxEntries)
            list.RemoveRange(IRecentFilesService.MaxEntries, list.Count - IRecentFilesService.MaxEntries);
        Save(list);
    }

    public void Remove(string path)
    {
        var list = new List<string>(Load());
        list.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
        Save(list);
    }

    public void Clear() => Save([]);

    private static void Save(List<string> list)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(list, s_opts));
    }

    private static string BuildPath()
    {
        string dir = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "dari")
            : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".config", "dari");

        return Path.Combine(dir, "recent.json");
    }
}
