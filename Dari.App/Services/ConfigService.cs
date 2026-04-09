using System.Runtime.InteropServices;
using System.Text.Json;
using Dari.App.Models;

namespace Dari.App.Services;

/// <summary>
/// Reads and writes <see cref="AppConfig"/> as JSON to the platform-appropriate path:
/// <list type="bullet">
///   <item>Linux / macOS — <c>~/.config/dari/config.json</c></item>
///   <item>Windows — <c>%LOCALAPPDATA%\dari\config.json</c></item>
/// </list>
/// The directory is created automatically on first write.
/// </summary>
public sealed class ConfigService : IConfigService
{
    private static readonly JsonSerializerOptions s_opts = new() { WriteIndented = true };

    /// <summary>Absolute path to the config file on the current platform.</summary>
    public static string ConfigPath { get; } = BuildConfigPath();

    /// <inheritdoc/>
    public AppConfig Load()
    {
        if (!File.Exists(ConfigPath))
            return new AppConfig();

        try
        {
            string json = File.ReadAllText(ConfigPath);
            return JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
        }
        catch
        {
            return new AppConfig();
        }
    }

    /// <inheritdoc/>
    public void Save(AppConfig config)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
        File.WriteAllText(ConfigPath, JsonSerializer.Serialize(config, s_opts));
    }

    private static string BuildConfigPath()
    {
        string dir = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "dari")
            : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".config", "dari");

        return Path.Combine(dir, "config.json");
    }
}
