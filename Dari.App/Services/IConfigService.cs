using Dari.App.Models;

namespace Dari.App.Services;

/// <summary>Reads and writes the application configuration file.</summary>
public interface IConfigService
{
    /// <summary>
    /// Loads the config from disk, or returns a fresh default if the file does not exist.
    /// </summary>
    AppConfig Load();

    /// <summary>Persists <paramref name="config"/> to disk.</summary>
    void Save(AppConfig config);
}
